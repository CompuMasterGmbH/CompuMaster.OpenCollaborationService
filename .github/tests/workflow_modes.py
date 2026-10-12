"""Checks actual dispatch expressions without credentials or server requests."""
import ast
from pathlib import Path
import re
import unittest

WORKFLOW = (Path(__file__).resolve().parents[1] / "workflows" / "BuildAndTest.yml").read_text(encoding="utf-8-sig")


def expression(line):
    return re.search(r"\$\{\{\s*(.*?)\s*\}\}", line).group(1)


def evaluate(value, isolated, cloud):
    # Only this workflow's literal/boolean expressions are supported, never arbitrary code.
    value = value.replace("inputs.isolated_only", str(isolated)).replace("inputs.cloud_versions_only", str(cloud))
    value = value.replace("&&", " and ").replace("||", " or ")
    value = re.sub(r"!(?=(?:True|False)\b)", " not ", value).strip()
    tree = ast.parse(value, mode="eval")
    allowed = (ast.Expression, ast.BoolOp, ast.UnaryOp, ast.And, ast.Or, ast.Not, ast.Constant)
    if any(not isinstance(node, allowed) for node in ast.walk(tree)):
        raise ValueError("Unsupported dispatch expression")
    return eval(compile(tree, "workflow mode", "eval"), {"__builtins__": {}})


class WorkflowModes(unittest.TestCase):
    def test_all_dispatch_combinations(self):
        opt_in = expression(next(line for line in WORKFLOW.splitlines() if "OCS_VERSION_TESTS_EXCLUSIVE:" in line))
        selection = expression(next(line.split("--filter", 1)[1] for line in WORKFLOW.splitlines() if "--filter" in line and "inputs.isolated_only" in line))
        for isolated in (False, True):
            for cloud in (False, True):
                with self.subTest(isolated=isolated, cloud=cloud):
                    enabled = evaluate(opt_in, isolated, cloud)
                    selected = evaluate(selection, isolated, cloud)
                    terms = [term.split("&", 1)[0].split("~", 1)[1] for term in selected.split("|")]
                    live_selected = any(term in "CompuMaster.Ocs.OwnCloudSharpTests.DavFileVersionLiveTests" for term in terms)
                    self.assertEqual(enabled, "1" if cloud and not isolated else "0")
                    if isolated:
                        self.assertFalse(live_selected, "Isolated dispatch must not select server acceptance")
                    elif cloud:
                        self.assertTrue(live_selected, "Cloud dispatch must actually verify versions")
                    self.assertTrue(any(term in "CompuMaster.Ocs.OwnCloudSharpTests.DavFileVersionClientTests" for term in terms))
                    self.assertTrue(any(term in "CompuMaster.Ocs.OwnCloudSharpTests.DavFileVersionEndpointTests" for term in terms))
                    if not isolated and not cloud:
                        self.assertIn("TestCategory!=EphemeralCloudVersions", selected)


if __name__ == "__main__":
    unittest.main()
