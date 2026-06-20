#!/usr/bin/env bash

# !!! BEFORE INVOKING THIS SCRIPT !!!
# Make sure you've created a python venv in the Archipelago directory, have activated it, and used it to install all of
# the necessary archipelago dependencies.
#
# It might be different on different platforms but it should look something like:
#
#   cd Archipelago
#   python3.13 -m venv .venv
#   source .venv/bin/activate
#   python ModuleUpdate.py
#
# The in future terminal sessions, remember to reactivate the venv:
#
#   source Archipelago/.venv/bin/activate

set -xeu

# Move to repository directory
cd "`dirname "$0"`/.."

rm -rf release
mkdir -p release/fezap

dotnet build --configuration Release -p:ModOutputDir=./release/fezap

# Generate FezAP.zip
cd release/fezap
zip -r ../FezAP.zip .
cd -
rm -rf release/fezap

# Generate fez.apworld and Fez.yaml
cd Archipelago
python3 Launcher.py "Build APWorlds" -- Fez --skip_open_folder
python3 Launcher.py "Generate Template Options" -- --skip_open_folder
cd -
cp Archipelago/build/apworlds/fez.apworld release/fez.apworld
cp Archipelago/Players/Templates/Fez.yaml release/Fez.yaml

echo "Make sure Fezap.cs and Metadata.xml have the correct version, tag your commit and double check your zips before uploading them."
