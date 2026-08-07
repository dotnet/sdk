import json
import sys
import xml.etree.ElementTree as ET


def local_name(element):
    return element.tag.rsplit("}", 1)[-1]


def child_text(element, name):
    if element is None:
        return ""
    for child in element.iter():
        if local_name(child) == name:
            return child.text or ""
    return ""


def test_definitions(root):
    definitions = {}
    for unit_test in root.iter():
        if local_name(unit_test) != "UnitTest":
            continue
        test_id = unit_test.attrib.get("id")
        method = next((node for node in unit_test.iter() if local_name(node) == "TestMethod"), None)
        if test_id and method is not None:
            class_name = method.attrib.get("className", "")
            method_name = method.attrib.get("name", "")
            definitions[test_id] = ".".join(part for part in (class_name, method_name) if part)
    return definitions


def result_summary(root):
    counters = next((node for node in root.iter() if local_name(node) == "Counters"), None)
    if counters is not None:
        return {
            key: int(counters.attrib.get(key, 0))
            for key in ("total", "executed", "passed", "failed", "error", "timeout", "aborted")
        }

    outcomes = [
        result.attrib.get("outcome", "").lower()
        for result in root.iter()
        if local_name(result) == "UnitTestResult"
    ]
    return {
        "total": len(outcomes),
        "executed": len(outcomes),
        "passed": outcomes.count("passed"),
        "failed": outcomes.count("failed"),
        "error": outcomes.count("error"),
        "timeout": outcomes.count("timeout"),
        "aborted": outcomes.count("aborted"),
    }


def failed_results(root):
    definitions = test_definitions(root)
    failures = []
    failed_outcomes = {"failed", "error", "timeout", "aborted"}
    for result in root.iter():
        if local_name(result) != "UnitTestResult":
            continue
        outcome = result.attrib.get("outcome", "")
        if outcome.lower() not in failed_outcomes:
            continue
        test_name = result.attrib.get("testName", "")
        failures.append({
            "testName": test_name,
            "fullyQualifiedName": definitions.get(result.attrib.get("testId"), test_name),
            "outcome": outcome,
            "duration": result.attrib.get("duration"),
            "errorMessage": child_text(result, "Message"),
            "stackTrace": child_text(result, "StackTrace"),
        })
    return failures


def main():
    root = ET.fromstring(sys.stdin.buffer.read())
    json.dump({"summary": result_summary(root), "failures": failed_results(root)}, sys.stdout)


if __name__ == "__main__":
    main()