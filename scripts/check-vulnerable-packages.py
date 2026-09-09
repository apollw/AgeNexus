"""Fail CI on reported NuGet vulnerabilities or an incomplete audit."""
import json
import sys
from pathlib import Path

report = json.loads(Path(sys.argv[1]).read_text())
issues = []


def visit(node):
    if isinstance(node, dict):
        if node.get("vulnerabilities"):
            issues.append({"package": node.get("id"), "version": node.get("resolvedVersion"),
                           "vulnerabilities": node["vulnerabilities"]})
        if str(node.get("level", "")).lower() in ("error", "warning"):
            issues.append({"audit_diagnostic": node})
        for value in node.values():
            visit(value)
    elif isinstance(node, list):
        for item in node:
            visit(item)


visit(report)
if not report.get("projects"):
    issues.append({"audit_diagnostic": "No projects were audited."})
print(json.dumps({"issues": issues}, indent=2))
sys.exit(bool(issues))
