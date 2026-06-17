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
  - Multi-line: pass JSON array syntax ["pat1","pat2"]
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
        printf '%s' "$s"
    else
        printf '%s...' "${s:0:$max_len}"
    fi
}

# Parse array syntax: ["a","b"] -> one element per line, or single value
parse_array_or_single() {
    local value="$1"
    value="$(printf '%s' "$value" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')"

    if [[ "$value" == \[*\] ]]; then
        local inner="${value:1:${#value}-2}"
        printf '%s' "$inner" | tr ',' '\n' | sed "s/^[[:space:]]*//;s/[[:space:]]*$//;s/^\"//;s/\"$//;s/^'//;s/'$//"
    else
        printf '%s\n' "$value"
    fi
}

# Case-insensitive fixed-string match (one line).
# Uses printf to avoid echo issues with lines starting with - or containing backslashes.
line_contains() {
    local line="$1"
    local substring="$2"
    printf '%s\n' "$line" | grep -qiF -- "$substring"
}

# Case-insensitive regex match (one line).
line_matches_regex() {
    local line="$1"
    local pattern="$2"
    printf '%s\n' "$line" | grep -qiE -- "$pattern"
}

# Read error text into array
mapfile -t lines <<< "$error_text"
line_count=${#lines[@]}

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
    for p in "${patterns[@]}"; do
        printf '  Pattern: /%s/\n' "$p"
    done
    echo "  Matching mode: Regex (case-insensitive)"
    echo "  Error text: ${line_count} lines"
    echo ""

    # Validate regex compilation (grep exit code 2 = regex error)
    for i in "${!patterns[@]}"; do
        set +e
        printf 'test\n' | grep -iE -- "${patterns[$i]}" >/dev/null 2>&1
        rc=$?
        set -e
        if [[ $rc -eq 2 ]]; then
            printf 'X INVALID REGEX at pattern [%d]: %s\n' "$i" "${patterns[$i]}"
            exit 1
        fi
    done
    echo "OK Regex compiles successfully"
    echo ""

    if [[ $pattern_count -eq 1 ]]; then
        match_count=0
        first_matches=""
        for i in "${!lines[@]}"; do
            line_num=$((i + 1))
            if line_matches_regex "${lines[$i]}" "${patterns[0]}"; then
                match_count=$((match_count + 1))
                if [[ $match_count -le 5 ]]; then
                    first_matches="${first_matches}$(printf '  Line %d: %s\n' "$line_num" "$(truncate_str "${lines[$i]}")")"$'\n'
                fi
            fi
        done

        if [[ $match_count -gt 0 ]]; then
            printf 'MATCH: Regex matched %d line(s)\n' "$match_count"
            printf '%s' "$first_matches"
            if [[ $match_count -gt 5 ]]; then
                printf '  ... and %d more\n' "$((match_count - 5))"
            fi

            if [[ $line_count -gt 10 ]]; then
                match_pct=$((match_count * 100 / line_count))
                if [[ $match_pct -gt 10 ]]; then
                    echo ""
                    printf 'WARNING: Pattern matched %d%% of all lines (%d/%d)\n' "$match_pct" "$match_count" "$line_count"
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
        matched_indices=()

        for start in "${!lines[@]}"; do
            if line_matches_regex "${lines[$start]}" "${patterns[0]}"; then
                all_match=true
                indices=("$start")
                search_from=$((start + 1))

                for p in $(seq 1 $((pattern_count - 1))); do
                    p_found=false
                    for line_idx in $(seq "$search_from" $((line_count - 1))); do
                        if line_matches_regex "${lines[$line_idx]}" "${patterns[$p]}"; then
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
            printf 'MATCH: All %d patterns matched in order\n' "$pattern_count"
            for p in "${!patterns[@]}"; do
                idx=${matched_indices[$p]}
                printf '  Line %d: %s\n' "$((idx + 1))" "$(truncate_str "${lines[$idx]}")"
            done
            exit 0
        else
            printf 'NO MATCH: Could not find all %d patterns in order\n' "$pattern_count"
            for p in "${!patterns[@]}"; do
                count=0
                for line in "${lines[@]}"; do
                    if line_matches_regex "$line" "${patterns[$p]}"; then
                        count=$((count + 1))
                    fi
                done
                if [[ $count -gt 0 ]]; then
                    printf '  Pattern [%d]: matched %d line(s)\n' "$p" "$count"
                else
                    printf '  Pattern [%d]: no matches\n' "$p"
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
    for m in "${messages[@]}"; do
        printf '  Pattern: "%s"\n' "$m"
    done
    echo "  Matching mode: String.Contains (case-insensitive)"
    echo "  Error text: ${line_count} lines"
    echo ""

    if [[ $msg_count -eq 1 ]]; then
        match_count=0
        first_matches=""
        for i in "${!lines[@]}"; do
            line_num=$((i + 1))
            if line_contains "${lines[$i]}" "${messages[0]}"; then
                match_count=$((match_count + 1))
                if [[ $match_count -le 5 ]]; then
                    first_matches="${first_matches}$(printf '  Line %d: %s\n' "$line_num" "$(truncate_str "${lines[$i]}")")"$'\n'
                fi
            fi
        done

        if [[ $match_count -gt 0 ]]; then
            printf 'MATCH: Found %d matching line(s)\n' "$match_count"
            printf '%s' "$first_matches"
            if [[ $match_count -gt 5 ]]; then
                printf '  ... and %d more\n' "$((match_count - 5))"
            fi

            if [[ $line_count -gt 10 ]]; then
                match_pct=$((match_count * 100 / line_count))
                if [[ $match_pct -gt 10 ]]; then
                    echo ""
                    printf 'WARNING: Pattern matched %d%% of all lines (%d/%d)\n' "$match_pct" "$match_count" "$line_count"
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
            if line_contains "${lines[$start]}" "${messages[0]}"; then
                all_match=true
                indices=("$start")
                search_from=$((start + 1))

                for p in $(seq 1 $((msg_count - 1))); do
                    p_found=false
                    for line_idx in $(seq "$search_from" $((line_count - 1))); do
                        if line_contains "${lines[$line_idx]}" "${messages[$p]}"; then
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
            printf 'MATCH: All %d patterns matched in order\n' "$msg_count"
            for p in "${!messages[@]}"; do
                idx=${matched_indices[$p]}
                printf '  Line %d: %s\n' "$((idx + 1))" "$(truncate_str "${lines[$idx]}")"
            done
            exit 0
        else
            printf 'NO MATCH: Could not find all %d patterns in order\n' "$msg_count"
            for p in "${!messages[@]}"; do
                count=0
                for line in "${lines[@]}"; do
                    if line_contains "$line" "${messages[$p]}"; then
                        count=$((count + 1))
                    fi
                done
                if [[ $count -gt 0 ]]; then
                    printf '  Pattern [%d]: matched %d line(s)\n' "$p" "$count"
                else
                    printf '  Pattern [%d]: no matches\n' "$p"
                fi
            done
            exit 1
        fi
    fi
fi
