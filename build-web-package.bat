@echo off
REM CardLister Web - Build Distributable Package
REM Creates a self-contained deployment for Windows

echo ================================
echo CardLister Web - Build Package
echo ================================
echo.

set OUTPUT_DIR=.\publish\CardLister-Web-Windows
set VERSION=2.0.1

echo Cleaning previous builds...
if exist publish rmdir /s /q publish
mkdir publish

echo.
echo Building self-contained package...
dotnet publish CardLister.Web\CardLister.Web.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=false ^
  -p:PublishReadyToRun=true ^
  -o %OUTPUT_DIR%

if errorlevel 1 (
    echo.
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo.
echo Creating launcher script...
(
echo @echo off
echo REM CardLister Web Application Launcher
echo.
echo set ASPNETCORE_URLS=http://0.0.0.0:5000
echo set ASPNETCORE_ENVIRONMENT=Production
echo.
echo echo =====================================
echo echo   CardLister Web Application
echo echo =====================================
echo echo.
echo echo Starting server on http://localhost:5000
echo echo Access from mobile: http://YOUR-IP:5000
echo echo.
echo echo Press Ctrl+C to stop the server
echo echo.
echo.
echo start http://localhost:5000
echo CardLister.Web.exe
) > %OUTPUT_DIR%\StartWeb.bat

echo.
echo Creating README...
(
echo # CardLister Web Application
echo.
echo ## Quick Start
echo.
echo 1. Double-click `StartWeb.bat` to start the server
echo 2. Your browser will open to http://localhost:5000
echo 3. Press Ctrl+C in the console window to stop
echo.
echo ## Mobile Access
echo.
echo 1. Find your computer's IP address:
echo    - Windows: Run `ipconfig` in Command Prompt
echo    - Look for "IPv4 Address" ^(e.g., 192.168.1.100^)
echo.
echo 2. On your phone/tablet ^(same Wi-Fi network^):
echo    - Open browser to: http://YOUR-IP:5000
echo    - Example: http://192.168.1.100:5000
echo.
echo ## Firewall Setup ^(Windows^)
echo.
echo To access from mobile devices, allow port 5000:
echo.
echo 1. Open Windows Defender Firewall
echo 2. Advanced Settings -^> Inbound Rules -^> New Rule
echo 3. Port -^> TCP -^> 5000 -^> Allow -^> Private
echo 4. Name: "CardLister Web"
echo.
echo Or run this PowerShell command ^(as Administrator^):
echo.
echo ```powershell
echo New-NetFirewallRule -DisplayName "CardLister Web" -Direction Inbound -LocalPort 5000 -Protocol TCP -Action Allow -Profile Private
echo ```
echo.
echo ## Database Location
echo.
echo The database is stored at:
echo `%%APPDATA%%\CardLister\cards.db`
echo.
echo This is shared with CardLister Desktop if installed.
echo.
echo ## Documentation
echo.
echo See `Docs\WEB-USER-GUIDE.md` for complete user guide.
echo See `Docs\DEPLOYMENT-GUIDE.md` for advanced deployment options.
echo.
echo ## Troubleshooting
echo.
echo **"Port 5000 already in use":**
echo - Another application is using port 5000
echo - Edit StartWeb.bat and change to port 8080 or 5001
echo.
echo **Can't access from phone:**
echo - Ensure same Wi-Fi network
echo - Check firewall allows port 5000
echo - Verify app is running ^(console window open^)
echo.
echo **Database errors:**
echo - Ensure %%APPDATA%%\CardLister directory exists
echo - Close CardLister Desktop if running
echo.
echo ## Version
echo.
echo CardLister Web v%VERSION%
echo Built: %DATE% %TIME%
) > %OUTPUT_DIR%\README.md

echo.
echo Copying documentation...
xcopy /Y /I Docs\WEB-USER-GUIDE.md %OUTPUT_DIR%\Docs\
xcopy /Y /I Docs\DEPLOYMENT-GUIDE.md %OUTPUT_DIR%\Docs\

echo.
echo Creating ZIP archive...
cd publish
powershell -Command "Compress-Archive -Path 'CardLister-Web-Windows' -DestinationPath 'CardLister-Web-Windows-v%VERSION%.zip' -Force"
cd ..

echo.
echo ================================
echo BUILD COMPLETE!
echo ================================
echo.
echo Package location: publish\CardLister-Web-Windows-v%VERSION%.zip
echo Package size:
dir publish\*.zip | find "CardLister-Web"
echo.
echo To test locally:
echo   cd publish\CardLister-Web-Windows
echo   StartWeb.bat
echo.
echo To distribute:
echo   1. Upload CardLister-Web-Windows-v%VERSION%.zip to GitHub Releases
echo   2. Users download, extract, and run StartWeb.bat
echo.

pause
