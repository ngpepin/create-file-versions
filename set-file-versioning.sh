#!/bin/bash

# Usage: ./set-file-versioning.sh <[nil]|enabled|disabled>

STATUS_FILE="/etc/default/file-versioning-state.txt"

# Function to display the current state
show_current_state() {
    if [ -f "$STATUS_FILE" ]; then
        current_state=$(cat "$STATUS_FILE")
        echo "Current file versioning state: $current_state"
    else
        echo "Status file not found. The current state is unknown."
    fi
}

# Check if an argument is provided
if [ "$#" -eq 0 ]; then
    show_current_state
    exit 0
fi

# Set the state based on the provided argument
if [ "$1" == "enabled" ] || [ "$1" == "disabled" ]; then
    echo "$1" > "$STATUS_FILE"
    echo "File versioning set to $1"
else
    echo "Invalid argument: $1. Use 'enabled' or 'disabled'."
    exit 1
fi
