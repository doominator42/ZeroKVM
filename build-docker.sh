#!/bin/sh
set -e

if [ $# -ne 1 ]; then
	1>&2 echo "one argument expected: target_arch"
	exit 1
fi

repo_dir=$(dirname "$(realpath "$0")")
if [ -z "$repo_dir" ]; then
	1>&2 echo "Error: could not find the repository path"
	exit 1
fi

mkdir -p "$repo_dir/bin"
sudo docker build -f "$repo_dir/src/Dockerfile.$1" --platform linux/arm64 --pull -o - "$repo_dir/src" | tar -C "$repo_dir/bin" -x --no-same-owner --overwrite
