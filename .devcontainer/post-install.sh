#/bin/bash

# Update apt
apt update

# Install mkdocs
apt install -y mkdocs

# Trust dotnet developer certs
dotnet dev-certs https --check --trust
