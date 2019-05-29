# SLOBSAudio
A quick and dirty volume changer for SL OBS audio devices.

## How do I use this?
Add --adv-settings to your Streamlabs OBS shortcut, open your settings, enable websockets and named pipes. Then open Streamlabs, then open streamdeck. Drag the tile across and select an audio device, press the key, and the volume should change :)

## Requirements
This plugin uses the native WebSocket support found in Windows 8 & higher. This means that any application that uses this library must be running Windows 8 or higher.

## Credits
This plugin uses https://github.com/TyrenDe/streamdeck-client-csharp/ to power it, along with https://github.com/StephenMP/SLOBSharp to communicate with SL OBS.

## License
MIT License

Copyright (c) 2019 Athene Allen

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
