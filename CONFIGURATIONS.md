# Status Bar Settings

## Implemented Features

### Settings Button
A settings button with a gear icon (⚙️) has been added to the right of the clock in the status bar.

### Dropdown Menu
Clicking the settings button displays a dropdown menu with the following options:

1. **Show on all displays**
   - When checked: The status bar appears on all monitors
   - When unchecked: The status bar appears only on the primary monitor
   - **Note:** This setting requires restarting the application to be applied

2. **Show/Hide Clock**
   - Controls the visibility of the clock in the status bar
   - Immediate effect

3. **Show/Hide Theme**
   - Controls the visibility of the theme toggle button
   - Immediate effect

### Settings Persistence
All settings are automatically saved to a JSON file (`settings.json`) located in the same directory as the application. Settings are:

- Automatically loaded when the application starts
- Automatically saved whenever an option is changed
- Applied immediately (except "Show on all displays")

### settings.json file structure
```json
{
  "ShowOnAllDisplays": true,
  "ShowClock": true,
  "ShowTheme": true
}
```

## Created/Modified Files

### New Files
- `Settings.cs` - Class to manage settings and JSON persistence

### Modified Files
- `MainWindow.xaml` - Added settings button and popup menu
- `MainWindow.xaml.cs` - Added logic to manage settings
- `App.xaml.cs` - Modified to respect the "Show on all displays" setting

## How to Use

1. Run the application
2. Click the gear button (⚙️) to the right of the clock
3. Check/uncheck the desired options
4. Settings are saved automatically
5. To apply "Show on all displays", restart the application

## Code Standards

- All code is written in English (variable names, comments, method names)
- User-facing text in the UI is in English
- Error messages and dialogs are in English
