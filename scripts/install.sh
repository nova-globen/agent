#!/usr/bin/env bash
# Install Agent Sync (agent + git-agent) from GitHub Releases on Linux/macOS.
#
# Usage:
#   ./install.sh            # install the latest release
#   ./install.sh v0.1.0     # install a specific version
#
# Or piped:
#   curl -fsSL https://raw.githubusercontent.com/nova-globen/agent/master/scripts/install.sh | bash
#   curl -fsSL https://raw.githubusercontent.com/nova-globen/agent/master/scripts/install.sh | bash -s -- v0.1.0
#
# Override the install directory:
#   AGENT_SYNC_INSTALL_DIR=/custom/bin ./install.sh
set -euo pipefail

REPO="nova-globen/agent"
VERSION="${1:-latest}"
INSTALL_DIR="${AGENT_SYNC_INSTALL_DIR:-$HOME/.agent-sync/bin}"

err() { echo "error: $*" >&2; exit 1; }
info() { echo "$*" >&2; }

need() { command -v "$1" >/dev/null 2>&1 || err "'$1' is required but not installed."; }

need curl
need tar
need uname
need mktemp

# Detect OS.
os_raw="$(uname -s)"
case "$os_raw" in
  Linux)  os="linux" ;;
  Darwin) os="osx" ;;
  *)      err "unsupported operating system: $os_raw (this installer supports Linux and macOS)." ;;
esac

# Detect architecture.
arch_raw="$(uname -m)"
case "$arch_raw" in
  x86_64 | amd64)         arch="x64" ;;
  arm64 | aarch64)        arch="arm64" ;;
  *)                      err "unsupported CPU architecture: $arch_raw." ;;
esac

rid="${os}-${arch}"

# Resolve "latest" to a concrete tag.
if [ "$VERSION" = "latest" ]; then
  info "Resolving latest release..."
  tag="$(curl -fsSL "https://api.github.com/repos/${REPO}/releases/latest" \
    | grep -m1 '"tag_name"' \
    | sed -E 's/.*"tag_name"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/')"
  [ -n "$tag" ] || err "could not determine the latest release tag."
else
  tag="$VERSION"
fi

archive="agent-sync-${tag}-${rid}.tar.gz"
url="https://github.com/${REPO}/releases/download/${tag}/${archive}"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

info "Downloading ${archive} ..."
if ! curl -fSL "$url" -o "$tmp/$archive"; then
  err "could not download ${url}. Check that release ${tag} has an asset for ${rid}."
fi

info "Extracting ..."
tar -C "$tmp" -xzf "$tmp/$archive" || err "failed to extract ${archive}."

[ -f "$tmp/agent" ] || err "archive did not contain 'agent'."
[ -f "$tmp/git-agent" ] || err "archive did not contain 'git-agent'."

mkdir -p "$INSTALL_DIR"
install -m 0755 "$tmp/agent" "$INSTALL_DIR/agent"
install -m 0755 "$tmp/git-agent" "$INSTALL_DIR/git-agent"

info ""
info "Installed Agent Sync ${tag} to ${INSTALL_DIR}:"
info "  $INSTALL_DIR/agent"
info "  $INSTALL_DIR/git-agent"

case ":$PATH:" in
  *":$INSTALL_DIR:"*)
    info ""
    info "Verify with: agent --version"
    ;;
  *)
    info ""
    info "Add it to your PATH (then restart your shell):"
    info "  export PATH=\"$INSTALL_DIR:\$PATH\""
    info ""
    info "Then verify with: agent --version  &&  git agent --version"
    ;;
esac
