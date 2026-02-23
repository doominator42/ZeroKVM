#!/bin/sh
set -e

# Usage:
# cat klids.txt | dl-kbd.sh output_dir > kbds.txt

if [ $# -ne 1 ]; then
	1>&2 echo "Expected one argument: output_dir"
	exit 1
fi

output_dir="$1"

while read -r line; do
	klid=$(echo -n "$line" | cut -f1)
	line_data=$(echo -n "$line" | cut -f2-)
	if [ -n "$klid" ]; then
		if ! echo -n "$klid" | grep -qP '^[0-9a-fA-F]{8}$'; then
			1>&2 echo "Invalid KLID: $klid"
			exit 1
		fi

		1>&2 echo "$klid: fetching file name ..."
		kbd_name=$(curl -s -w '%{redirect_url}\n' -o /dev/null "https://kbdlayout.info/$klid/" | sed 's/^.*\///')
		if [ -z "$kbd_name" ]; then
			1>&2 echo "KLID $klid not found"
			exit 1
		fi

		printf '%s\t%s\t%s\n' "$klid" "$kbd_name" "$line_data"
		if [ -e "$output_dir/$kbd_name.xml" ]; then
			1>&2 echo "$klid: file exists: $kbd_name"
		else
			1>&2 echo "$klid: downloading layout $kbd_name ..."
			curl -s -o "$output_dir/$kbd_name.xml" "https://kbdlayout.info/$kbd_name/download/xml"
		fi
	fi

	1>&2 echo "Waiting ..."
	sleep 15
done
