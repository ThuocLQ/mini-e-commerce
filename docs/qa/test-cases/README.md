# Manual QA Test Cases

`storefront-browser-qa-test-cases.csv` is the versioned import source for Excel, TestRail, Xray or Azure DevOps Test Plans. It is intentionally kept as CSV so reviewers can diff it in Git. Open it in Excel, save an execution copy as `.xlsx`, and retain execution-only fields such as tester, run date, actual result and defect ID outside the source file.

Evidence is stored under `../evidence/`. Each case links by a stable relative path and must be referenced by test-case ID in a test report.