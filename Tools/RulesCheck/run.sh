#!/usr/bin/env bash
# 게임 규칙 점검 (Unity 없이). Unity 에디터에 들어 있는 C# 컴파일러와 .NET 런타임을 쓴다.
# 사용: bash Tools/RulesCheck/run.sh [튜토리얼 맵 폴더]   (기본: ../Executor-Server/docs/maps/tutorial)
# 결과물은 Tools/RulesCheck/bin/ (git에 올리지 않음)
set -euo pipefail
cd "$(dirname "$0")/../.."

UNITY_VERSION=$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt | tr -d '\r')
E="${UNITY_EDITOR_DATA:-/c/Program Files/Unity/Hub/Editor/$UNITY_VERSION/Editor/Data}"
DOTNET="$E/NetCoreRuntime/dotnet.exe"
NJ=$(ls -d Library/PackageCache/com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll | head -1)
RUNTIME=$(ls "$E/NetCoreRuntime/shared/Microsoft.NETCore.App" | head -1)
OUT=Tools/RulesCheck/bin
mkdir -p "$OUT"

# Unity에 의존하지 않는 코드만 컴파일한다 (Game, Data, Network/Json)
"$DOTNET" "$E/DotNetSdkRoslyn/csc.dll" -nologo -langversion:9.0 -nostdlib -nowarn:1701 -target:exe \
  -out:"$OUT/RulesCheck.dll" \
  -r:"$E/NetStandard/ref/2.1.0/netstandard.dll" -r:"$NJ" \
  Tools/RulesCheck/Program.cs Assets/Scripts/Game/*.cs Assets/Scripts/Data/*.cs Assets/Scripts/Network/Json.cs

cp "$NJ" "$OUT/"
cat > "$OUT/RulesCheck.runtimeconfig.json" <<EOF
{ "runtimeOptions": { "tfm": "net6.0", "framework": { "name": "Microsoft.NETCore.App", "version": "$RUNTIME" } } }
EOF

"$DOTNET" "$OUT/RulesCheck.dll" "${1:-../Executor-Server/docs/maps/tutorial}"
