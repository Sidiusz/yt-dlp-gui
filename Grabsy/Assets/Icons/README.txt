Grabsy icon placeholders.

Replace these PNGs (and grabsy.ico) with the real artwork — file names + sizes
must stay the same so the build keeps wiring them in.

Files
  grabsy.ico        Multi-resolution icon used by:
                      * Grabsy.exe (set via <ApplicationIcon> in Grabsy.csproj)
                      * AppWindow taskbar/titlebar icon (MainWindow SetupWindow)
                      * Settings window icon
                      * System tray icon (MainWindow SetupTray)
                    Should contain 16, 24, 32, 48, 64, 128, 256 px frames.

  grabsy-16.png     16x16   small UI uses
  grabsy-24.png     24x24
  grabsy-32.png     32x32   Main + Settings window titlebar logo
  grabsy-48.png     48x48
  grabsy-64.png     64x64   Tray menu header logo (displayed at 32 DIPs)
  grabsy-128.png    128x128
  grabsy-256.png    256x256
  grabsy-1024.png   1024x1024
  grabsy-2048.png   2048x2048

To rebuild grabsy.ico from new PNGs use any ICO packer (ImageMagick):
  magick grabsy-16.png grabsy-24.png grabsy-32.png grabsy-48.png ^
         grabsy-64.png grabsy-128.png grabsy-256.png grabsy.ico
