#!/bin/bash

# This script recursively searches for versioned files in a specified directory
# and lists or deletes them based on their age or irrespective of their age.
# A versioned file is defined as a file whose name follows the pattern '.*~~~~*'.
#
# Usage:
#   ./purge-file-versions.sh <directory>  [-h|--help] [[days] | [-a|--all]] [-d|--delete] [--notrash]"
#
# Arguments:
#   <directory>   : The directory path where the script will search for versioned files.
#   [days]        : Optional. Integer representing the age of the file in days. Defaults to 1 day.
#                   - ignored if -a or --all is provided."
#   [-a|--all]    : Optional. If provided, the script will target all versioned files,
#                   irrespective of their age."
#   [-d|--delete] : Optional. If provided, the script will delete the files it finds.
#   [--notrash]   : Permanently delete instead of sending to the trash.
#                   irrespective of their age.
# Examples:
#   List versioned files older than 30 days in a specific directory:
#     ./purge-file-versions.sh /path/to/directory 30
#
#   Delete all versioned files in a specific directory:
#     ./purge-file-versions.sh /path/to/directory --all --delete
#
# Warning:
#   Use the delete option with --notrash with caution, as it will permanently remove files.
#
# Dependencies:
#   trash-cli (https://github.com/andreafrancia/trash-cli/issues)
#

DIRECTORY=$1
DEFAULT_DIR='/mnt/drive2/nextcloud/local-cache/Files'

print_help() {
    echo "This script recursively searches for versioned files in a specified directory"
    echo "and lists or deletes them based on their age or irrespective of their age."
    echo "Please refer to documentation on comand 'create-file-versions' for more context."
    echo ""
    echo "Usage: $0 <directory> [-h|--help] [[days] | [-a|--all]] [-d|--delete] [--notrash] [-q|--quiet]"
    echo ""
    echo "Arguments:"
    echo ""
    echo "   <directory>   : The directory path where the script will search for versioned files."
    echo "   [days]        : Optional. Integer representing the age of the file in days. Defaults to 1 day."
    echo "                   - ignored if -a or --all is provided."
    echo "   [-a|--all]    : Optional. If provided, the script will target all versioned files,"
    echo "                   irrespective of their age."
    echo "   [-d|--delete] : Optional. If provided, the script will delete the files it finds."
    echo "   [--notrash]   : Permanently delete instead of sending to the trash*."
    echo "   [-q|--quiet]  : Optional. If provided, makes script output less verbose."
    echo ""
    echo "*Note that this script depends on trash-cli being installed."
}

# Function to check if input is an integer
is_integer() {
    [[ $1 =~ ^-?[0-9]+$ ]]
}

### MAIN 
something_found=false
age_provided=false

# Check if the second parameter is an integer
if is_integer "$2"; then
    AGE_DAYS="$2"
    something_found=true
    age_provided=true
# If not, check if the first parameter is an integer
elif is_integer "$1"; then
    AGE_DAYS="$1"
    something_found=true
    age_provided=true
# Otherwise, set AGE_DAYS to 1
else
    AGE_DAYS=1
fi

DELETE_FLAG=false
ALL_FLAG=false
NO_TRASH_FLAG=false
QUIET_FLAG=false

dir_found=false
if [ -z "$DIRECTORY" ] || [ ! -d "$DIRECTORY" ]; then
   DIRECTORY="$DEFAULT_DIR"
else
   something_found=true
   dir_found=true
   shift # Move past the first argument (a valid directory)   
fi

# Parse other arguments
while (( "$#" )); do
    case "$1" in
        -d|--delete)
            DELETE_FLAG=true
	    something_found=true
            shift
            ;;
	--notrash)
            NO_TRASH_FLAG=true
	    something_found=true
            shift
            ;;
        -a|--all)
            ALL_FLAG=true
	    something_found=true
            shift
            ;;
        -q|--quiet)
            QUIET_FLAG=true
	    something_found=true
            shift
            ;;
	-h|--help)
            print_help
	    exit 0
            ;;
        *)
            shift
            ;;
    esac
done

echo ""

if [[ $dir_found == false ]]; then
    if [[ $something_found == true ]]; then
       if [[ $DELETE_FLAG == true ]]; then
          if [[ $QUIET_FLAG == false ]]; then
	     echo -e "\033[33m **** WARNING *** \033[0m No valid directory provided, assuming $DIRECTORY"
          fi      
       else 
          echo -e "No valid directory provided, assuming $DIRECTORY"
       fi
    else
       echo -e "\033[31m *** ERROR *** \033[0m No valid parameters provided, displaying help."
       echo ""
       print_help
       exit 1
    fi
fi

if [[ $ALL_FLAG == false ]]; then
   if [[ $QUIET_FLAG == false && $AGE_DAYS -ne 0 ]]; then
      echo "The following results will be for version files that are older than the last 24 hours."   
      echo "Specify an integer number for the minimum age in [days] or [-a|--all] to include everything."
      echo ""
   fi
fi

# Check if trash-cli is installed
if [[ $DELETE_FLAG == true && $NO_TRASH_FLAG == false ]]; then
      if command -v /usr/bin/trash-put >/dev/null 2>&1; then
      	 if [[ $QUIET_FLAG == false ]]; then
	     echo "Confirmed that trash-cli is installed, so safe deletion can be performed."
	 fi
      else
          echo ""
	  echo -e "\033[33m **** WARNING *** \033[0m trash-cli is NOT installed or is NOT in \/usr\/bin.\033[33m Adding an implicit '--notrash' option!\033[0m"
	  if [[ $QUIET_FLAG == false ]]; then
	      echo "If this is an interactive session you will be prompted to proceed."
	      echo "Please see https://github.com/andreafrancia/trash-cli to address going forward."
	  fi
	  NO_TRASH_FLAG=true
      fi
fi
	    
# This is aafe-guard in case '--notrash' option was chosen and this is an interactive session
if [[ $DELETE_FLAG == true && $NO_TRASH_FLAG == true ]]; then
   if [[ $QUIET_FLAG == false ]]; then
      echo ""
      echo -e "\033[33m **** WARNING *** \033[0m will be proceeding with DELETION with NOTRASH enabled!" 
   fi
   
   # Check if the script is running interactively
   if [ -n "$PS1" ]; then
       is_interactive=1
   else
       is_interactive=0
   fi
   # Check if the script is being sourced
   if [ "$0" != "$BASH_SOURCE" ]; then
       is_sourced=1
   else
       is_sourced=0
   fi
   # Check if the script is attached to a terminal
   if [ -t 0 ]; then
       is_attached_to_terminal=1
   else
       is_attached_to_terminal=0
   fi

   # echo "is_interactive $is_interactive"
   # echo "is_sourced $is_sourced"
   # echo "is_attached_to_terminal $is_attached_to_terminal"
   
   # Execute the main body only if not sourced, interactive, and attached to a terminal
   if [ $is_attached_to_terminal -eq 1 ]; then
 
      if [[ $QUIET_FLAG == false ]]; then
         echo ""
         read -p "Do you want to continue with permanently deleting matching version files? (y/N): " response

         # Default to 'N' if no response is given
         response=${response:-N}

         # Convert to uppercase for easier comparison
         response=$(echo "$response" | tr '[:lower:]' '[:upper:]')

         # Check the response
         if [ "$response" = "N" ]; then
             NO_TRASH_FLAG=false
             echo "Using the trash instead."
         fi
      fi
   fi 
fi   
     
echo ""

# Function to find and optionally delete files
process_files() {
    local dir=$1
    local days=$2
    local _delete=$3
    local notrash=$4
    local _all=$5
    
    local delete=false
    local all=false
    if [ "$_delete" = true ]; then
        delete=true
    fi
    if [ "$_all" = true ]; then
        all=true
    fi
    
    local did_find=false
    local deleted_files=false
    local parsefile=""
 
    if [[ $all == true && $age_provided == true ]]; then
       if [[ $QUIET_FLAG == false ]]; then
          echo "Ignoring the provided [days] parameter because -a or --all was specified."    
       fi
       days=0 
    fi
    
    local find_cmd="find \"$dir\" -type f"
    
    if [[ $all == false ]]; then
	if [[ $days -eq 0 ]]; then
	    echo "Here are ALL the version files, including those created/modified within the last 24 hours:" 
	    find_cmd="$find_cmd -mtime -1"
	else   
	    echo "Here are version files that are at least $days day(s) old:" 
            find_cmd="$find_cmd -mtime +$days"
   	fi
    else
	 if [[ $QUIET_FLAG == false ]]; then
	   echo "Here are ALL the version files, regardless of their creation or modification dates:" 
    	else
	   echo "Here are ALL the version files:"
	fi
    fi
    echo "--------------------------------------------------------------------------------------------------"

    # Exclude files containing '.trashinfo'
    find_cmd="$find_cmd ! -path '*\.trashinfo*'"
    
    # Exclude files containing '.trashinfo'
    find_cmd="$find_cmd ! -path '*\.Trash*'"

    # Include files matching the specified name pattern
    find_cmd="$find_cmd -name '.*~~~~*'"

    # Append xargs command for ls
    find_cmd="$find_cmd | xargs -d '\n' ls -ld"
    #echo "Find command: $find_cmd"
       
    did_find=false
    deleted_files=false
    
    while read -r file; do
        echo "$file"

        parsefile="${file#*/}"
        if [ "$parsefile" = "$file" ]; then
           parsefile="."
        fi
	
	if [ -z "$parsefile" ]; then
           echo "*** Could not parse filename"
	else
	   if [[ $parsefile != "." ]]; then
	      # echo "   Found ~~~/$parsefile~~~"
	      did_find=true
	      if [[ $delete == true ]]; then
   
		  if [[ $notrash == true ]]; then
		       rm "/$parsefile"
		       if [[ $QUIET_FLAG == false ]]; then
		          echo -e  " ->\033[33m Deleted /$parsefile \033[0m"
		       fi
	          else
                       /usr/bin/trash-put "/$parsefile"
		       if [[ $QUIET_FLAG == false ]]; then
		          echo " -> Trashed /$parsefile"
		       fi
		  fi
	          deleted_files=true	
	      fi
	   fi
	    
         fi
	 
    done < <(eval $find_cmd)
    
    echo ""
    if [[ $did_find == false ]]; then
    	echo "No version files found." 
    fi
    if [[ $delete == true && $deleted_files == true ]]; then
       if [[ $QUIET_FLAG == false ]]; then
          if [[ $notrash == true ]]; then
             echo -e "\033[31m PERMANENTLY DELETED matching version files. \033[0m"
          else
             echo "Sent matching version files to the trash."
          fi
       fi    
    fi	   
}


# MAIN CONTINUED
# Call the function with provided arguments
process_files "$DIRECTORY" "$AGE_DAYS" "$DELETE_FLAG" "$NO_TRASH_FLAG" "$ALL_FLAG"

