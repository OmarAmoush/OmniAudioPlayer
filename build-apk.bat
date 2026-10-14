@echo off
chcp 65001 >nul
echo =========================================================
echo   بناء وتوقيع تطبيق مشغل الصوتيات الشامل (.NET MAUI APK)
echo =========================================================

if not exist omniaudio-release.keystore (
    echo [1/3] توليد مفتاح التوقيع الرقمي Keystore...
    keytool -genkeypair -v -keystore omniaudio-release.keystore -alias omniaudio -keyalg RSA -keysize 2048 -validity 10000 -storepass OmniAudio123! -keypass OmniAudio123! -dname "CN=OmniAudioPlayer, OU=Mobile, O=OmniAudio, L=Cairo, S=EG, C=EG"
)

echo [2/3] تثبيت حزم .NET MAUI وبناء ملف الـ APK الموقع...
dotnet publish OmniAudioPlayer/OmniAudioPlayer.csproj -f net8.0-android -c Release -p:AndroidPackageFormat=apk -p:AndroidKeyStore=true -p:AndroidSigningKeyStore="%cd%\omniaudio-release.keystore" -p:AndroidSigningKeyAlias=omniaudio -p:AndroidSigningKeyPass=OmniAudio123! -p:AndroidSigningStorePass=OmniAudio123! -o ./OutputAPK

echo [3/3] اكتمل البناء بنجاح! ملف الـ APK الجاهز للتثبيت موجود داخل مجلد OutputAPK
pause
