#!/usr/bin/env bash
set -euo pipefail

echo "=== Workshop post-create setup ==="

# Ensure .NET SDK matches global.json (10.0.100 prerelease)
echo "--- Installed SDKs ---"
dotnet --list-sdks || true

# Install the Aspire CLI as a global tool
if ! dotnet tool list --global | grep -q "aspire.cli"; then
  echo "--- Installing Aspire CLI (--prerelease) ---"
  dotnet tool install --global Aspire.Cli --prerelease
fi

# Make sure the global tools directory is on PATH for future shells
if ! grep -q '.dotnet/tools' ~/.bashrc; then
  echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.bashrc
fi
export PATH="$PATH:$HOME/.dotnet/tools"

# Trust the ASP.NET Core HTTPS dev certificate
echo "--- Trusting dev-certs ---"
dotnet dev-certs https --trust || true

# Restore packages so the first build is fast
echo "--- Restoring packages ---"
dotnet restore eShop.Web.slnf

# Install Playwright browsers for the e2e tests (chromium only, keeps it slim)
if [ -f package.json ]; then
  echo "--- Installing npm deps + Playwright chromium ---"
  npm ci
  npx playwright install chromium
fi

echo "=== Setup complete ==="
echo ""
echo "Start Aspire:"
echo "  aspire run"
echo ""
echo "Dashboard: http://localhost:19888 (auto-forwarded)"
