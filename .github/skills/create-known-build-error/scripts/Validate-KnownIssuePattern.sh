#!/usr/bin/env bash
# Validate-KnownIssuePattern.sh
# Tests a Known Build Error pattern (ErrorMessage or ErrorPattern) against sample error text.
#
# Usage:
#   ./Validate-KnownIssuePattern.sh --error-text "the actual error text" --error-message "substring"
#   ./Validate-KnownIssuePattern.sh --error-text-file errors.txt --error-pattern "regex"

set -euo pipefail

error_text=""
error_text_file=""
error_message=""
error_pattern=""

print_usage() {
    cat <<'EOF'
Validate-KnownIssuePattern: Test a Known Build Error pattern against error text.

Usage:
  ./Validate-KnownIssuePattern.sh --error-text "<text>" --error-message "<message>"
  ./Validate-KnownIssuePattern.sh --error-text-file <file> --error-pattern "<regex>"

Options:
  --error-text <text>       The actual error text to match against
  --error-text-file <file>  Load error text from a file
  --error-message <msg>     ErrorMessage value (String.Contains matching, case-insensitive)
  --error-pattern <regex>   ErrorPattern value (Regex matching, case-insensitive)
  --help                    Show this help

The script validates that the pattern matches the error text using the same
matching rules as the Build Analysis system:
  - ErrorMessage: case-insensitive substring match, per line
  - ErrorPattern: case-insensitive regex match, per line
  - Multi-line: pass pipe-separated values with --multi flag
EOF
}

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        --error-text)
            error_text="$2"; shift 2 ;;
        --error-text-file)
            error_text_file="$2"; shift 2 ;;
        --error-message)
            error_message="$2"; shift 2 ;;
        --error-pattern)
            error_pattern="$2"; shift 2 ;;
        --help)
            print_usage; exit 0 ;;
        *)
            echo "Unknown option: $1" >&2; print_usage; exit 1 ;;
    esac
done

# Load error text from file if specified
if [[ -n "$error_text_file" ]]; then
    if [[ ! -f "$error_text_file" ]]; then
        echo "Error text file not found: $error_text_file" >&2
        exit 1
    fi
    error_text=$(cat "$error_text_file")
fi

if [[ -z "$error_text" ]]; then
    echo "Error: --error-text or --error-text-file is required." >&2
    print_usage
    exit 1
fi

if [[ -z "$error_message" && -z "$error_pattern" ]]; then
    echo "Error: either --error-message or --error-pattern is required." >&2
    print_usage
    exit 1
fi

truncate_str() {
    local s="$1"
    local max_len="${2:-200}"
    if [[ ${#s} -le $max_len ]]; then
        echo "$s"
    else
        echo "${s:0:$max_len}..."
    fi
}

# Parse array syntax: ["a","b"] -> separate elements, or single value
parse_array_or_single() {
    local value="$1"
    value=$(echo "$value" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')

    if [[ "$value" == \[*\] ]]; then
        # Strip brackets, split on comma, trim quotes
        local inner="${value:1:${#value}-2}"
        echo "$inner" | tr ',' '\n' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//;s/^"//;s/"$//;s/^'\''//;s/'\''$//'
    else
        echo "$value"
    fi
}

# ErrorPattern takes priority (same as build-insights)
if [[ -n "$error_pattern" ]]; then
    mapfile -t patterns < <(parse_array_or_single "$error_pattern")
    pattern_count=${#patterns[@]}

    if [[ $pattern_count -eq 1 ]]; then
        mode_label="single-line"
    else
        mode_label="${pattern_count}-line"
    fi

    echo "Validating ErrorPattern ($mode_label)"
    pattern_display=$(printf '/%s/ ' "${patterns[@]}")
    echo "  Pattern(s): ${pattern_display% }"
    echo "  Matching mode: Regex (case-insensitive)"

    mapfile -t lines <<< "$error_text"
    echo "  Error text: ${#lines[@]} lines"
    echo ""

    # Validate regex compilation
    for i in "${!patterns[@]}"; do
        if ! echo "" | grep -qiE "${patterns[$i]}" 2>/dev/null; then
            # Try the pattern - if grep returns error (not just no match), it's invalid
            echo "test" | grep -iE "${patterns[$i]}" > /dev/null 2>&1
            grep_exit=$?
            if [[ $grep_exit -eq 2 ]]; then
                echo "X INVALID REGEX at pattern [$i]: ${patterns[$i]}"
                exit 1
            fi
        fi
    done
    echo "OK Regex compiles successfully"
    echo ""

    if [[ $pattern_count -eq 1 ]]; then
        match_count=0
        matched_lines=""
        for i in "${!lines[@]}"; do
            line_num=$((i + 1))
            if echo "${lines[$i]}" | grep -qiE "${patterns[0]}"; then
                match_count=$((match_count + 1))
                if [[ $match_count -le 5 ]]; then
                    matched_lines="${matched_lines}  Line ${line_num}: $(truncate_str "${lines[$i]}")\n"
                fi
            fi
        done

        if [[ $match_count -gt 0 ]]; then
            echo "MATCH: Regex matched ${match_count} line(s)"
            echo -e "$matched_lines"
            if [[ $match_count -gt 5 ]]; then
                echo "  ... and $((match_count - 5)) more"
            fi

            total_lines=${#lines[@]}
            if [[ $total_lines -gt 10 ]]; then
                match_pct=$((match_count * 100 / total_lines))
                if [[ $match_pct -gt 10 ]]; then
                    echo ""
                    echo "WARNING: Pattern matched ${match_pct}% of all lines (${match_count}/${total_lines})"
                    echo "  This pattern may be too generic. Consider making it more specific."
                fi
            fi
            exit 0
        else
            echo "NO MATCH: Regex did not match any line"
            exit 1
        fi
    else
        # Multi-line: patterns must appear in order (not necessarily consecutive)
        found=false
        match_start=-1
        matched_indices=()

        for start in "${!lines[@]}"; do
            if echo "${lines[$start]}" | grep -qiE "${patterns[0]}"; then
                all_match=true
                indices=("$start")
                search_from=$((start + 1))

                for p in $(seq 1 $((pattern_count - 1))); do
                    p_found=false
                    for line_idx in $(seq $search_from $((${#lines[@]} - 1))); do
                        if echo "${lines[$line_idx]}" | grep -qiE "${patterns[$p]}"; then
                            indices+=("$line_idx")
                            search_from=$((line_idx + 1))
                            p_found=true
                            break
                        fi
                    done
                    if [[ "$p_found" != "true" ]]; then
                        all_match=false
                        break
                    fi
                done

                if [[ "$all_match" == "true" ]]; then
                    found=true
                    matched_indices=("${indices[@]}")
                    break
                fi
            fi
        done

        if [[ "$found" == "true" ]]; then
            echo "MATCH: All ${pattern_count} patterns matched in order"
            for p in "${!patterns[@]}"; do
                idx=${matched_indices[$p]}
                echo "  Line $((idx + 1)): $(truncate_str "${lines[$idx]}")"
            done
            exit 0
        else
            echo "NO MATCH: Could not find all ${pattern_count} patterns in order"
            for p in "${!patterns[@]}"; do
                count=0
                for line in "${lines[@]}"; do
                    if echo "$line" | grep -qiE "${patterns[$p]}"; then
                        count=$((count + 1))
                    fi
                done
                if [[ $count -gt 0 ]]; then
                    echo "  Pattern [$p]: matched $count line(s)"
                else
                    echo "  Pattern [$p]: no matches"
                fi
            done
            exit 1
        fi
    fi
else
    # ErrorMessage mode (substring contains, case-insensitive)
    mapfile -t messages < <(parse_array_or_single "$error_message")
    msg_count=${#messages[@]}

    if [[ $msg_count -eq 1 ]]; then
        mode_label="single-line"
    else
        mode_label="${msg_count}-line"
    fi

    echo "Validating ErrorMessage ($mode_label)"
    msg_display=$(printf '"%s" ' "${messages[@]}")
    echo "  Pattern(s): ${msg_display% }"
    echo "  Matching mode: String.Contains (case-insensitive)"

    mapfile -t lines <<< "$error_text"
    echo "  Error text: ${#lines[@]} lines"
    echo ""

    if [[ $msg_count -eq 1 ]]; then
        # Case-insensitive substring search using grep -iF (fixed string)
        match_count=0
        matched_lines=""
        for i in "${!lines[@]}"; do
            line_num=$((i + 1))
            if echo "${lines[$i]}" | grep -qiF "${messages[0]}"; then
                match_count=$((match_count + 1))
                if [[ $match_count -le 5 ]]; then
                    matched_lines="${matched_lines}  Line ${line_num}: $(truncate_str "${lines[$i]}")\n"
                fi
            fi
        done

        if [[ $match_count -gt 0 ]]; then
            echo "MATCH: Found ${match_count} matching line(s)"
            echo -e "$matched_lines"
            if [[ $match_count -gt 5 ]]; then
                echo "  ... and $((match_count - 5)) more"
            fi

            total_lines=${#lines[@]}
            if [[ $total_lines -gt 10 ]]; then
                match_pct=$((match_count * 100 / total_lines))
                if [[ $match_pct -gt 10 ]]; then
                    echo ""
                    echo "WARNING: Pattern matched ${match_pct}% of all lines (${match_count}/${total_lines})"
                    echo "  This pattern may be too generic. Consider making it more specific."
                fi
            fi
            exit 0
        else
            echo "NO MATCH: ErrorMessage not found in any line"
            exit 1
        fi
    else
        # Multi-line: messages must appear in order (not necessarily consecutive)
        found=false
        matched_indices=()

        for start in "${!lines[@]}"; do
            if echo "${lines[$start]}" | grep -qiF "${messages[0]}"; then
                all_match=true
                indices=("$start")
                search_from=$((start + 1))

                for p in $(seq 1 $((msg_count - 1))); do
                    p_found=false
                    for line_idx in $(seq $search_from $((${#lines[@]} - 1))); do
                        if echo "${lines[$line_idx]}" | grep -qiF "${messages[$p]}"; then
                            indices+=("$line_idx")
                            search_from=$((line_idx + 1))
                            p_found=true
                            break
                        fi
                    done
                    if [[ "$p_found" != "true" ]]; then
                        all_match=false
                        break
                    fi
                done

                if [[ "$all_match" == "true" ]]; then
                    found=true
                    matched_indices=("${indices[@]}")
                    break
                fi
            fi
        done

        if [[ "$found" == "true" ]]; then
            echo "MATCH: All ${msg_count} patterns matched in order"
            for p in "${!messages[@]}"; do
                idx=${matched_indices[$p]}
                echo "  Line $((idx + 1)): $(truncate_str "${lines[$idx]}")"
            done
            exit 0
        else
            echo "NO MATCH: Could not find all ${msg_count} patterns in order"
            for p in "${!messages[@]}"; do
                count=0
                for line in "${lines[@]}"; do
                    if echo "$line" | grep -qiF "${messages[$p]}"; then
                        count=$((count + 1))
                    fi
                done
                if [[ $count -gt 0 ]]; then
                    echo "  Pattern [$p]: matched $count line(s)"
                else
                    echo "  Pattern [$p]: no matches"
                fi
            done
            exit 1
        fi
    fi
fi
