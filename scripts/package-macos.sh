#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly REPOSITORY_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly PROJECT_PATH="${REPOSITORY_ROOT}/src/Fusebox/Fusebox.csproj"
readonly ICON_SOURCE="${REPOSITORY_ROOT}/src/Fusebox/Assets/fusebox-icon.png"
readonly OUTPUT_ROOT="${REPOSITORY_ROOT}/artifacts/publish"
readonly CONFIGURATION="${CONFIGURATION:-Release}"
readonly APP_VERSION="${APP_VERSION:-1.0.0}"
readonly BUNDLE_IDENTIFIER="com.thomasvhoutum.fusebox"

usage() {
  cat <<'EOF'
Usage: scripts/package-macos.sh [all|osx-arm64|osx-x64]

Builds signed-independent Fusebox.app bundles for macOS. The default is "all".

Optional environment variables:
  CONFIGURATION  .NET build configuration (default: Release)
  APP_VERSION    Bundle version in numeric dotted form (default: 1.0.0)
EOF
}

require_macos_tool() {
  local tool_name="$1"

  if ! command -v "${tool_name}" >/dev/null 2>&1; then
    echo "Required macOS tool not found: ${tool_name}" >&2
    exit 1
  fi
}

create_icon() {
  local resources_directory="$1"
  local work_directory="$2"
  local iconset_directory="${work_directory}/Fusebox.iconset"

  mkdir -p "${iconset_directory}"

  sips -z 16 16 "${ICON_SOURCE}" --out "${iconset_directory}/icon_16x16.png" >/dev/null
  sips -z 32 32 "${ICON_SOURCE}" --out "${iconset_directory}/icon_16x16@2x.png" >/dev/null
  sips -z 32 32 "${ICON_SOURCE}" --out "${iconset_directory}/icon_32x32.png" >/dev/null
  sips -z 64 64 "${ICON_SOURCE}" --out "${iconset_directory}/icon_32x32@2x.png" >/dev/null
  sips -z 128 128 "${ICON_SOURCE}" --out "${iconset_directory}/icon_128x128.png" >/dev/null
  sips -z 256 256 "${ICON_SOURCE}" --out "${iconset_directory}/icon_128x128@2x.png" >/dev/null
  sips -z 256 256 "${ICON_SOURCE}" --out "${iconset_directory}/icon_256x256.png" >/dev/null
  sips -z 512 512 "${ICON_SOURCE}" --out "${iconset_directory}/icon_256x256@2x.png" >/dev/null
  sips -z 512 512 "${ICON_SOURCE}" --out "${iconset_directory}/icon_512x512.png" >/dev/null
  sips -z 1024 1024 "${ICON_SOURCE}" --out "${iconset_directory}/icon_512x512@2x.png" >/dev/null

  iconutil \
    --convert icns \
    --output "${resources_directory}/Fusebox.icns" \
    "${iconset_directory}"
}

create_info_plist() {
  local plist_path="$1"

  cat >"${plist_path}" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDisplayName</key>
  <string>Fusebox</string>
  <key>CFBundleExecutable</key>
  <string>Fusebox</string>
  <key>CFBundleIconFile</key>
  <string>Fusebox.icns</string>
  <key>CFBundleIdentifier</key>
  <string>${BUNDLE_IDENTIFIER}</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>Fusebox</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>${APP_VERSION}</string>
  <key>CFBundleVersion</key>
  <string>${APP_VERSION}</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
EOF
}

package_runtime() {
  local runtime_identifier="$1"
  local runtime_output="${OUTPUT_ROOT}/${runtime_identifier}"
  local publish_directory="${runtime_output}/publish"
  local app_directory="${runtime_output}/Fusebox.app"
  local contents_directory="${app_directory}/Contents"
  local executable_directory="${contents_directory}/MacOS"
  local resources_directory="${contents_directory}/Resources"
  local work_directory="${runtime_output}/packaging"

  echo "Publishing Fusebox for ${runtime_identifier}..."

  rm -rf "${publish_directory}" "${app_directory}" "${work_directory}"
  mkdir -p "${publish_directory}" "${executable_directory}" "${resources_directory}" "${work_directory}"

  dotnet publish "${PROJECT_PATH}" \
    --configuration "${CONFIGURATION}" \
    --runtime "${runtime_identifier}" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    --output "${publish_directory}"

  cp -R "${publish_directory}/." "${executable_directory}/"
  chmod +x "${executable_directory}/Fusebox"

  create_icon "${resources_directory}" "${work_directory}"
  create_info_plist "${contents_directory}/Info.plist"
  plutil -lint "${contents_directory}/Info.plist" >/dev/null

  rm -rf "${publish_directory}" "${work_directory}"

  echo "Created ${app_directory}"
}

main() {
  local target="${1:-all}"

  if [[ "${target}" == "-h" || "${target}" == "--help" ]]; then
    usage
    exit 0
  fi

  if [[ ! "${APP_VERSION}" =~ ^[0-9]+(\.[0-9]+)*$ ]]; then
    echo "APP_VERSION must be a numeric dotted version such as 1.2.0." >&2
    exit 1
  fi

  require_macos_tool dotnet
  require_macos_tool sips
  require_macos_tool iconutil
  require_macos_tool plutil

  case "${target}" in
    all)
      package_runtime osx-arm64
      package_runtime osx-x64
      ;;
    osx-arm64|osx-x64)
      package_runtime "${target}"
      ;;
    *)
      usage >&2
      exit 1
      ;;
  esac
}

main "$@"
