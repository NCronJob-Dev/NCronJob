#!/usr/bin/env bash
# Fails when the public API surface of src/NCronJob changes without a matching update to docs/llms-full.md.
# Only effectively public API counts: public/protected members of internal (or non-public nested) types are ignored,
# because consumers must not rely on them.
# Usage: check-llms-full.sh <base-ref>
set -euo pipefail

base_ref="${1:?usage: check-llms-full.sh <base-ref>}"
merge_base="$(git merge-base "$base_ref" HEAD)"
llms_file="docs/llms-full.md"

# Prints the trimmed public/protected declarations of a C# source that are reachable from outside the assembly.
extract_public_api() {
  awk '
    function indent_of(line) { match(line, /^[ \t]*/); return RLENGTH }
    {
      line = $0
      depth = indent_of(line)
      is_type = line ~ /^[ \t]*((public|internal|private|protected|file|sealed|static|abstract|partial|readonly|ref|unsafe|new)[ \t]+)*(class|struct|interface|enum|record)[ \t]/
      is_public = line ~ /^[ \t]*(public|protected)[ \t]/

      if (is_type) {
        while (top > 0 && stack_depth[top] >= depth) top--
        parent_visible = (top == 0) ? 1 : stack_visible[top]
        top++
        stack_depth[top] = depth
        stack_visible[top] = (is_public && parent_visible) ? 1 : 0
      }

      if (is_public && top > 0 && stack_visible[top]) {
        sub(/^[ \t]+/, "", line)
        print line
      }
    }
  '
}

api_of_revision() {
  local revision="$1" file
  git diff --name-only "$merge_base" HEAD -- 'src/NCronJob/*.cs' | while read -r file; do
    if git cat-file -e "$revision:$file" 2>/dev/null; then
      git show "$revision:$file" | extract_public_api
    fi
  done | sort
}

# A declaration that only moved between files is not an API change, so compare the two sides as multisets.
api_changes="$(diff <(api_of_revision "$merge_base") <(api_of_revision HEAD) \
  | grep -E '^[<>]' | sed -E 's/^< /- /; s/^> /+ /' || true)"

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
