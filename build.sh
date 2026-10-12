#!/bin/sh
# Linux(Mono)でのビルド用
cd "$(dirname "$0")"; mkdir -p bin
R=""; for f in res/emoji/*.png; do n=$(basename $f); R="$R -resource:$f,micopy.emoji.$n"; done
mcs -langversion:5 -sdk:4.8 -target:winexe -optimize+ -codepage:utf8 -out:bin/micopy.exe -win32icon:res/micopy.ico \
 -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Xml.Linq.dll -r:System.Xml.dll -r:System.Core.dll \
 -resource:res/micopy.ico,micopy.ico -resource:res/logo.png,micopy.logo.png $R src/*.cs
