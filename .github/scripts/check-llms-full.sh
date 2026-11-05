#!/usr/bin/env bash
# Fails when the public API surface of src/NCronJob changes without a matching update to docs/llms-full.md.
# Usage: check-llms-full.sh <base-ref>
set -euo pipefail

base_ref="${1:?usage: check-llms-full.sh <base-ref>}"
merge_base="$(git merge-base "$base_ref" HEAD)"
llms_file="docs/llms-full.md"

api_changes="$(git diff --unified=0 "$merge_base" HEAD -- 'src/NCronJob/*.cs' \
  | grep -E '^[+-][[:space:]]*(public|protected)[[:space:]]' \
  | grep -vE '^[+-]{3} ' || true)"

if [[ -z "$api_changes" ]]; then
  echo "No public API changes detected."
  exit 0
fi

if git diff --name-only "$merge_base" HEAD -- "$llms_file" | grep -q .; then
  echo "Public API changed and $llms_file was updated."
  exit 0
fi

echo "::error file=$llms_file::The public API changed but $llms_file was not updated."
echo "Changed public API lines:"
echo "$api_changes"
echo
echo "Update $llms_file (and docs/agent-guide.md if relevant), or add the 'skip-llms-check' label if the change needs no documentation."
exit 1
