# Default: native host platform (arm64 on Apple Silicon, amd64 elsewhere).
#
# If you hit a Grpc.Tools arm64 `protoc` segfault (exit 139) during build,
# uncomment the `--platform=linux/amd64` line below AND the matching
# `build.options` + `runArgs` entries in devcontainer.json. This forces the
# container to amd64 so x64 protoc runs through Rosetta. Works, but ~20–40%
# slower on Apple Silicon.
# FROM --platform=linux/amd64 mcr.microsoft.com/devcontainers/dotnet:10.0
FROM mcr.microsoft.com/devcontainers/dotnet:10.0
