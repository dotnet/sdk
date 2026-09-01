// Usage: dotnet run analyze-watch-test-output.cs <input-log-file-or-url> [output-html-file]
// Parses xUnit test output from dotnet watch, groups interleaved lines by test name,
// and generates an HTML page with collapsible test output sections.
// The first argument can be a local file path or a URL. If a URL is provided,
// the file is downloaded to a temp directory before processing.

using System.Text;
using System.Text.RegularExpressions;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: dotnet run analyze-watch-test-output.cs <input-log-file-or-url> [output-html-file]");
    return 1;
}

string inputFile;
string? tempFile = null;

if (Uri.TryCreate(args[0], UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
{
    try
    {
        using var httpClient = new HttpClient();
        Console.Error.WriteLine($"Downloading {args[0]}...");
        var bytes = httpClient.GetByteArrayAsync(uri).GetAwaiter().GetResult();
        var fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "downloaded-log.txt";
        tempFile = Path.Combine(Path.GetTempPath(), fileName);
        File.WriteAllBytes(tempFile, bytes);
        inputFile = tempFile;
        Console.Error.WriteLine($"Downloaded to {tempFile}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: Failed to download {args[0]}: {ex.Message}");
        return 1;
    }
}
else
{
    inputFile = args[0];
}

if (!File.Exists(inputFile))
{
    Console.Error.WriteLine($"Error: File not found: {inputFile}");
    return 1;
}

var outputFile = args.Length > 1 ? args[1] : Path.ChangeExtension(inputFile, ".html");

try
{
    var lines = File.ReadAllLines(inputFile);
    var tests = ParseTestOutput(lines);
    var html = GenerateHtml(tests, inputFile);
    File.WriteAllText(outputFile, html, Encoding.UTF8);
    Console.WriteLine($"Wrote {outputFile} ({tests.Count} tests, {lines.Length} input lines)");
    return 0;
}
finally
{
    if (tempFile != null && File.Exists(tempFile))
        File.Delete(tempFile);
}

// --- Parsing ---

List<TestInfo> ParseTestOutput(string[] allLines)
{
    // [xUnit.net HH:MM:SS.ss]     TestName [TAG] content
    var testLineRegex = new Regex(
        @"^\[xUnit\.net\s+([^\]]+)\]\s{4,}(.+?)\s+\[(PASS|FAIL|SKIP|OUTPUT)\]\s?(.*)?$");

    // [xUnit.net HH:MM:SS.ss]       continuation (6+ spaces, no tag — e.g. skip reason)
    var continuationRegex = new Regex(
        @"^\[xUnit\.net\s+([^\]]+)\]\s{6,}(.+)$");

    // [xUnit.net HH:MM:SS.ss] assembly: [Long Running Test] 'TestName', Elapsed: ...
    var longRunningRegex = new Regex(
        @"^\[xUnit\.net\s+([^\]]+)\]\s+\S+:\s+\[Long Running Test\]\s+'([^']+)'");

    // Non-xunit result lines:   Passed/Failed/Skipped TestName [time]
    var resultRegex = new Regex(
        @"^\s+(Passed|Failed|Skipped)\s+(.+?)\s+\[");

    var testMap = new Dictionary<string, TestInfo>(StringComparer.Ordinal);
    string? lastTestName = null;

    TestInfo GetOrCreate(string name)
    {
        if (!testMap.TryGetValue(name, out var info))
        {
            info = new TestInfo(name);
            testMap[name] = info;
        }
        return info;
    }

    for (int i = 0; i < allLines.Length; i++)
    {
        var line = allLines[i];

        // 1. Continuation line (6+ spaces) — check before test-specific to avoid
        //    capturing indented content (e.g. "[OUTPUT] ...") as a bogus test name.
        var m = continuationRegex.Match(line);
        if (m.Success)
        {
            if (lastTestName != null)
            {
                var info = GetOrCreate(lastTestName);
                info.Lines.Add(m.Groups[2].Value);
            }
            continue;
        }

        // 2. Test-specific xUnit line with tag
        m = testLineRegex.Match(line);
        if (m.Success)
        {
            var timestamp = m.Groups[1].Value;
            var testName = m.Groups[2].Value;
            var tag = m.Groups[3].Value;
            var content = m.Groups[4].Value;

            var info = GetOrCreate(testName);
            lastTestName = testName;

            if (tag.Equals("PASS", StringComparison.OrdinalIgnoreCase))
            {
                info.Status = TestStatus.Passed;
                info.Lines.Add("[PASS]");
            }
            else if (tag.Equals("FAIL", StringComparison.OrdinalIgnoreCase))
            {
                info.Status = TestStatus.Failed;
                info.Lines.Add("[FAIL]");
            }
            else if (tag.Equals("SKIP", StringComparison.OrdinalIgnoreCase))
            {
                info.Status = TestStatus.Skipped;
                info.Lines.Add("[SKIP]");
            }
            else
            {
                // [OUTPUT] and other tags — show just the content
                info.Lines.Add(content);
            }
            continue;
        }

        // 3. Long Running Test line — skip
        if (longRunningRegex.IsMatch(line))
            continue;

        // 4. Non-xunit result line (Passed/Failed/Skipped)
        m = resultRegex.Match(line);
        if (m.Success)
        {
            var result = m.Groups[1].Value;
            var testName = m.Groups[2].Value;
            var info = GetOrCreate(testName);
            // Extract duration from e.g. "  Passed TestName [67 ms]"
            var durationMatch = Regex.Match(line, @"\[([^\]]+)\]\s*$");
            var duration = durationMatch.Success ? durationMatch.Groups[1].Value : "";
            info.Lines.Add($"{result} [{duration}]");

            if (result.Equals("Passed", StringComparison.OrdinalIgnoreCase) && info.Status == TestStatus.Unknown)
                info.Status = TestStatus.Passed;
            else if (result.Equals("Failed", StringComparison.OrdinalIgnoreCase))
                info.Status = TestStatus.Failed;
            else if (result.Equals("Skipped", StringComparison.OrdinalIgnoreCase) && info.Status == TestStatus.Unknown)
                info.Status = TestStatus.Skipped;
            continue;
        }
    }

    // Sort: failed first, then skipped, then unknown/running, then passed
    var sorted = testMap.Values.ToList();
    sorted.Sort((a, b) =>
    {
        int Order(TestStatus s) => s switch
        {
            TestStatus.Failed => 0,
            TestStatus.Skipped => 1,
            TestStatus.Unknown => 2,
            TestStatus.Passed => 3,
            _ => 4
        };
        int cmp = Order(a.Status).CompareTo(Order(b.Status));
        return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
    });

    return sorted;
}

// --- HTML Generation ---

string GenerateHtml(List<TestInfo> tests, string sourceFile)
{
    int passed = tests.Count(t => t.Status == TestStatus.Passed);
    int failed = tests.Count(t => t.Status == TestStatus.Failed);
    int skipped = tests.Count(t => t.Status == TestStatus.Skipped);
    int unknown = tests.Count(t => t.Status == TestStatus.Unknown);

    var sb = new StringBuilder();
    sb.AppendLine("<!DOCTYPE html>");
    sb.AppendLine("<html lang=\"en\">");
    sb.AppendLine("<head>");
    sb.AppendLine("<meta charset=\"UTF-8\">");
    sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
    sb.AppendLine($"<title>Test Output: {HtmlEncode(Path.GetFileName(sourceFile))}</title>");
    sb.AppendLine("<style>");
    sb.AppendLine("""
        * { box-sizing: border-box; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 0; padding: 20px; background: #1e1e1e; color: #d4d4d4; }
        h1 { font-size: 1.4em; margin-bottom: 10px; }
        .stats { margin-bottom: 16px; font-size: 0.95em; }
        .stats span { margin-right: 16px; }
        .s-passed { color: #4ec9b0; }
        .s-failed { color: #f44747; }
        .s-skipped { color: #dcdcaa; }
        .s-unknown { color: #9cdcfe; }
        .controls { margin-bottom: 16px; }
        .controls button { background: #333; color: #d4d4d4; border: 1px solid #555; padding: 6px 14px; cursor: pointer; margin-right: 8px; border-radius: 4px; }
        .controls button:hover { background: #444; }
        details { margin-bottom: 4px; border: 1px solid #333; border-radius: 4px; }
        details[open] { margin-bottom: 8px; }
        summary { cursor: default; padding: 8px 12px; font-family: monospace; font-size: 0.9em; list-style: none; display: flex; align-items: center; }
        summary::-webkit-details-marker { display: none; }
        summary .toggle { cursor: pointer; user-select: none; margin-right: 6px; font-size: 0.8em; flex-shrink: 0; }
        summary .badge { cursor: pointer; }
        summary .test-name { user-select: text; cursor: text; }
        summary:hover { background: #2a2a2a; }
        .badge { display: inline-block; padding: 1px 7px; border-radius: 3px; font-size: 0.8em; margin-right: 8px; font-family: sans-serif; }
        .b-passed { background: #1a3a2a; color: #4ec9b0; }
        .b-failed { background: #3a1a1a; color: #f44747; }
        .b-skipped { background: #3a3a1a; color: #dcdcaa; }
        .b-unknown { background: #1a2a3a; color: #9cdcfe; }
        .line-count { color: #888; font-size: 0.85em; margin-left: 8px; }
        pre { margin: 0; padding: 12px; background: #1a1a1a; overflow-x: auto; font-size: 0.82em; line-height: 1.5; border-top: 1px solid #333; }
        .output { margin: 0; padding: 12px; background: #1a1a1a; overflow-x: auto; font-size: 0.82em; border-top: 1px solid #333; font-family: monospace; }
        .output .line { white-space: pre; line-height: 1.5; margin: 0; padding: 0; }
        .output .line.no-emoji { color: #ffffff; }
        .output .test-marker { color: #b5894e; }
        .file-group { margin: 0; padding: 0; }
        .file-group summary { padding: 0; font-family: monospace; font-size: 1em; list-style: none; display: inline; }
        .file-group summary::-webkit-details-marker { display: none; }
        .file-group summary::before { content: '[+] '; color: #569cd6; }
        .file-group[open] summary::before { content: '[-] '; color: #569cd6; }
        .file-group .file-list { padding-left: 2em; color: #888; margin: 0; }
        .file-group .file-list .line { white-space: pre; line-height: 1.5; }
        .filter-bar { margin-bottom: 12px; }
        .filter-bar input { background: #2a2a2a; color: #d4d4d4; border: 1px solid #555; padding: 6px 12px; width: 400px; max-width: 100%; border-radius: 4px; font-size: 0.9em; }
    """);
    sb.AppendLine("</style>");
    sb.AppendLine("</head>");
    sb.AppendLine("<body>");
    sb.AppendLine($"<h1>Test Output: {HtmlEncode(Path.GetFileName(sourceFile))}</h1>");

    sb.AppendLine("<div class=\"stats\">");
    sb.AppendLine($"  <span><strong>{tests.Count}</strong> tests</span>");
    if (passed > 0) sb.AppendLine($"  <span class=\"s-passed\">✅ {passed} passed</span>");
    if (failed > 0) sb.AppendLine($"  <span class=\"s-failed\">❌ {failed} failed</span>");
    if (skipped > 0) sb.AppendLine($"  <span class=\"s-skipped\">⏭ {skipped} skipped</span>");
    if (unknown > 0) sb.AppendLine($"  <span class=\"s-unknown\">⏳ {unknown} unknown</span>");
    sb.AppendLine("</div>");

    sb.AppendLine("<div class=\"filter-bar\"><input type=\"text\" id=\"filter\" placeholder=\"Filter tests by name...\" oninput=\"filterTests()\"></div>");

    sb.AppendLine("<div class=\"controls\">");
    sb.AppendLine("  <button onclick=\"toggleAll(true)\">Expand All</button>");
    sb.AppendLine("  <button onclick=\"toggleAll(false)\">Collapse All</button>");
    sb.AppendLine("</div>");

    sb.AppendLine("<div id=\"test-list\">");
    foreach (var test in tests)
    {
        var badgeClass = test.Status switch
        {
            TestStatus.Passed => "b-passed",
            TestStatus.Failed => "b-failed",
            TestStatus.Skipped => "b-skipped",
            _ => "b-unknown"
        };
        var badgeText = test.Status switch
        {
            TestStatus.Passed => "PASS",
            TestStatus.Failed => "FAIL",
            TestStatus.Skipped => "SKIP",
            _ => "???"
        };
        var openAttr = test.Status == TestStatus.Failed ? " open" : "";
        var arrow = test.Status == TestStatus.Failed ? "▼" : "▶";

        sb.AppendLine($"<details class=\"test-entry\" data-name=\"{HtmlAttrEncode(test.Name)}\"{openAttr}>");
        sb.AppendLine($"  <summary onclick=\"event.preventDefault()\"><span class=\"toggle\" onclick=\"this.parentElement.parentElement.toggleAttribute('open');\">{arrow}</span><span class=\"badge {badgeClass}\" onclick=\"this.parentElement.parentElement.toggleAttribute('open');\">{badgeText}</span><span class=\"test-name\">{HtmlEncode(test.Name)}</span><span class=\"line-count\">({test.Lines.Count} lines)</span></summary>");
        sb.AppendLine("<div class=\"output\">");
        RenderTestLines(sb, test.Lines);
        sb.AppendLine("</div>");
        sb.AppendLine("</details>");
    }
    sb.AppendLine("</div>");

    sb.AppendLine("<script>");
    sb.AppendLine("""
        function toggleAll(open) {
            document.querySelectorAll('#test-list details.test-entry').forEach(d => {
                d.open = open;
                d.querySelector('.toggle').textContent = open ? '▼' : '▶';
            });
        }
        function filterTests() {
            const q = document.getElementById('filter').value.toLowerCase();
            document.querySelectorAll('.test-entry').forEach(d => {
                d.style.display = d.dataset.name.toLowerCase().includes(q) ? '' : 'none';
            });
        }
        // Update toggle arrows on state change
        new MutationObserver(muts => {
            muts.forEach(m => {
                if (m.attributeName === 'open') {
                    const t = m.target.querySelector('.toggle');
                    if (t) t.textContent = m.target.open ? '▼' : '▶';
                }
            });
        }).observe(document.getElementById('test-list'), { subtree: true, attributes: true, attributeFilter: ['open'] });
    """);
    sb.AppendLine("</script>");
    sb.AppendLine("</body>");
    sb.AppendLine("</html>");
    return sb.ToString();
}

void RenderTestLines(StringBuilder sb, List<string> lines)
{
    var watchingRegex = new Regex(@"Watching \d+ file\(s\) for changes");
    var fileLineRegex = new Regex(@">\s+\S");
    var solutionRegex = new Regex(@"Solution after (project|document) update:");
    var solutionChildRegex = new Regex(@"(Project:|Document:|Additional:|Config:)\s");

    int i = 0;
    while (i < lines.Count)
    {
        if (watchingRegex.IsMatch(lines[i]))
        {
            i = RenderCollapsibleGroup(sb, lines, i, fileLineRegex);
        }
        else if (solutionRegex.IsMatch(lines[i]))
        {
            i = RenderCollapsibleGroup(sb, lines, i, solutionChildRegex);
        }
        else
        {
            // Collapse runs of more than 3 identical consecutive lines
            int runLength = 1;
            while (i + runLength < lines.Count && lines[i + runLength] == lines[i])
                runLength++;

            if (runLength > 3)
            {
                sb.AppendLine($"<details class=\"file-group\"><summary>{HtmlEncode(lines[i])} <span class=\"line-count\">(×{runLength} repeated)</span></summary><div class=\"file-list\">");
                for (int k = 0; k < runLength; k++)
                    sb.AppendLine(RenderLine(lines[i]));
                sb.AppendLine("</div></details>");
                i += runLength;
            }
            else
            {
                sb.AppendLine(RenderLine(lines[i]));
                i++;
            }
        }
    }
}

int RenderCollapsibleGroup(StringBuilder sb, List<string> lines, int i, Regex childRegex)
{
    var headerLine = lines[i];
    var children = new List<string>();
    int j = i + 1;
    while (j < lines.Count && childRegex.IsMatch(lines[j]))
    {
        children.Add(lines[j]);
        j++;
    }

    if (children.Count > 0)
    {
        sb.AppendLine($"<details class=\"file-group\"><summary>{HtmlEncode(headerLine)}</summary><div class=\"file-list\">");
        foreach (var child in children)
            sb.AppendLine(RenderLine(child));
        sb.AppendLine("</div></details>");
        return j;
    }

    sb.AppendLine(RenderLine(lines[i]));
    return i + 1;
}

static string HtmlEncode(string s) => s
    .Replace("&", "&amp;")
    .Replace("<", "&lt;")
    .Replace(">", "&gt;")
    .Replace("\"", "&quot;");

static string HtmlAttrEncode(string s) => HtmlEncode(s).Replace("'", "&#39;");

static bool HasEmoji(string s)
{
    foreach (var rune in s.EnumerateRunes())
    {
        int v = rune.Value;
        if ((v >= 0x2300 && v <= 0x27BF) || (v >= 0x2900 && v <= 0x2BFF) ||
            (v >= 0x2600 && v <= 0x26FF) || (v >= 0x1F300 && v <= 0x1F9FF))
            return true;
    }
    return false;
}

static string RenderLine(string line)
{
    if (Regex.IsMatch(line, @"^\[TEST [^\]]+:\d+\]"))
        return $"<div class=\"line test-marker\">{HtmlEncode(line)}</div>";
    var cls = HasEmoji(line) ? "line" : "line no-emoji";
    return $"<div class=\"{cls}\">{HtmlEncode(line)}</div>";
}

// --- Types ---

enum TestStatus { Unknown, Passed, Failed, Skipped }

class TestInfo(string name)
{
    public string Name { get; } = name;
    public List<string> Lines { get; } = [];
    public TestStatus Status { get; set; } = TestStatus.Unknown;
}
