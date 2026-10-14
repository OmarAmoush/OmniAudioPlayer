#!/usr/bin/env bash
set -e
echo "========================================================="
echo "  بناء وتوقيع تطبيق مشغل الصوتيات الشامل (.NET MAUI APK)"
echo "========================================================="

if [ ! -f omniaudio-release.keystore ]; then
  echo "[1/3] توليد مفتاح التوقيع الرقمي Keystore..."
  keytool -genkeypair -v \
    -keystore omniaudio-release.keystore \
    -alias omniaudio \
    -keyalg RSA -keysize 2048 -validity 10000 \
    -storepass OmniAudio123! -keypass OmniAudio123! \
    -dname "CN=OmniAudioPlayer, OU=Mobile, O=OmniAudio, L=Cairo, S=EG, C=EG"
fi

echo "[2/3] بناء وتوقيع ملف الـ APK..."
dotnet publish OmniAudioPlayer/OmniAudioPlayer.csproj \
  -f net8.0-android -c Release \
  -p:AndroidPackageFormat=apk \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore="$(pwd)/omniaudio-release.keystore" \
  -p:AndroidSigningKeyAlias=omniaudio \
  -p:AndroidSigningKeyPass=OmniAudio123! \
  -p:AndroidSigningStorePass=OmniAudio123! \
  -o ./OutputAPK

echo "[3/3] اكتمل البناء! ملف الـ APK الموقع جاهز داخل مجلد ./OutputAPK"
