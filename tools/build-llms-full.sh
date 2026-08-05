#!/usr/bin/env bash
# Concatenates every documentation page referenced by docs/_sidebar.md
# into docs/llms-full.txt, in sidebar order. CI fails when the output is stale.
set -euo pipefail

cd "$(dirname "$0")/.."

output="docs/llms-full.txt"
base_url="https://joaoaalves.github.io/Truss"

{
    echo "# Truss, full documentation"
    echo
    echo "> Generated from the pages listed in docs/_sidebar.md. The index with per-page summaries is at $base_url/llms.txt."
} > "$output"

grep -o '([A-Za-z-]*\.md)' docs/_sidebar.md | tr -d '()' | while read -r page; do
    {
        echo
        echo "---"
        echo
        echo "Source: $base_url/$page"
        echo
        cat "docs/$page"
    } >> "$output"
done

echo "wrote $output"
