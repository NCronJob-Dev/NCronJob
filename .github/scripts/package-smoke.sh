#!/usr/bin/env bash
# Packs NCronJob and NCronJob.Dashboard, publishes a host that consumes the packages
# for every target framework, and checks that the dashboard and its embedded assets are served.
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
host="$root/tests/PackageSmoke"
version="0.0.0-smoke"
port=5390

rm -rf "$host/nupkgs" "$host/bin" "$host/obj" "$host/publish"
for project in NCronJob NCronJob.Dashboard; do
  dotnet pack "$root/src/$project/$project.csproj" -c Release -o "$host/nupkgs" -p:Version="$version"
done

pid=""
trap '[ -n "$pid" ] && kill "$pid" 2>/dev/null || true' EXIT

expect() {
  local url="$1" type="$2" actual
  actual="$(curl -fsS -o /dev/null -w '%{content_type}' "$url")"
  [[ "$actual" == "$type"* ]] || { echo "::error::$url served '$actual', expected '$type'"; return 1; }
  echo "ok $url ($actual)"
}

for tf in $(dotnet msbuild "$host" -getProperty:TargetFrameworks | tr ';' ' '); do
  out="$host/publish/$tf"
  dotnet publish "$host" -c Release -f "$tf" -o "$out" -p:NCronJobVersion="$version"

  dotnet "$out/PackageSmoke.dll" --urls "http://127.0.0.1:$port" > "$out/host.log" 2>&1 &
  pid=$!
  base="http://127.0.0.1:$port/ncronjob"
  for _ in $(seq 1 30); do curl -fs -o /dev/null "$base" && break; sleep 1; done

  echo "== $tf"
  expect "$base" "text/html" \
    && expect "$base/_framework/blazor.web.js" "text/javascript" \
    && expect "$base/logo.png" "image/png" \
    || { cat "$out/host.log"; exit 1; }

  kill "$pid"; wait "$pid" 2>/dev/null || true; pid=""
done
