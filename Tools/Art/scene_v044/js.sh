#!/bin/sh
# usage: js.sh <port> < script.js   (stdin keeps UTF-8 intact on Windows)
cat > /tmp/js_body.$$; curl -s --max-time 900 -X POST --data-binary @/tmp/js_body.$$ http://127.0.0.1:$1/; rm -f /tmp/js_body.$$; echo
