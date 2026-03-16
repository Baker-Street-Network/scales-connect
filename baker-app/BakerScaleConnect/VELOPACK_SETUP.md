# Velopack Setup Guide for BakerScaleConnect

## What Was Configured

Velopack has been integrated into your BakerScaleConnect application with the following changes:

### 1. NuGet Package Added
- Added `Velopack` package to `BakerScaleConnect.csproj`

### 2. Application Startup Integration
- Added `VelopackApp.Build().Run()` in `Program.cs` before application initialization
- This handles installation, uninstallation, and update events automatically

## Next Steps

### ⚠️ Complete the Manual Step Above First!

Once you've added the UpdateService line to Program.cs, you're ready to create your first release.

## Important Notes

- **Automatic Updates**: Updates are downloaded automatically but applied on next manual restart
- **Update Check Interval**: Every 4 hours (configurable)
- **First Check Delay**: 30 seconds after app startup

## Testing Your Setup

### End-to-End Test

1. **Complete the manual step** (add UpdateService to Program.cs)
2. **Commit all changes** to your repository
3. **Create your first release**:
   ```powershell
   git tag v1.0.0
   git push origin v1.0.0
   ```
4. **Wait for GitHub Actions** to build and publish (check Actions tab)
5. **Download the installer** from GitHub Releases
6. **Install on a test machine**
7. **Make a code change** and increment version
8. **Create second release**:
   ```powershell
   git tag v1.0.1
   git push origin v1.0.1
   ```
9. **Check the logs** on the test machine to see update detection
10. **Restart the app** to apply the update

## Useful Commands

```powershell
# Create and push a new release tag
git tag v1.0.0
git push origin v1.0.0

# View all tags
git tag -l

# Delete a tag (if needed)
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0

# Check Velopack installation
vpk --version

# Manually build a release (if needed)
dotnet publish -c Release
vpk pack -u BakerScaleConnect -v 1.0.0 -p .\bin\Release\net8.0-windows\publish -e BakerScaleConnect.exe
```

## Resources

- [Velopack Documentation](https://docs.velopack.io/)
- [Velopack GitHub](https://github.com/velopack/velopack)
- [Quick Start Guide](https://docs.velopack.io/getting-started/)

## Current Integration Status

✅ Velopack NuGet package added
✅ Velopack initialization code added to Program.cs
✅ Automatic update service created (Services/UpdateService.cs)
✅ GitHub Actions workflow created (.github/workflows/release.yml)
⚠️ **MANUAL STEP NEEDED**: Add one line to Program.cs (see below)

## Manual Step Required

Due to a file lock, you need to manually add the UpdateService to Program.cs:

**In Program.cs, after line 33 (after the WebServerHostedService registration), add:**

```csharp
// Register the update service
services.AddHostedService<UpdateService>();
```

The code should look like this:
```csharp
// Register the background service
services.AddHostedService<BakerScaleBackgroundService>();

// Register the web server hosted service
services.AddHostedService<WebServerHostedService>();

// Register the update service
services.AddHostedService<UpdateService>();  // <-- ADD THIS LINE

// Register scanner manager as singleton
services.AddSingleton<ScannerManager>();
```

## How Automatic Updates Work

The `UpdateService` runs in the background and:
- Waits 30 seconds after app startup
- Checks for updates every 4 hours
- Downloads updates automatically from GitHub Releases
- Updates are applied on the next manual app restart
- All update activity is logged

## How to Create and Publish Releases

### Method 1: Using Git Tags (Recommended)

1. Commit your changes
2. Create and push a version tag:
   ```powershell
   git tag v1.0.0
   git push origin v1.0.0
   ```
3. GitHub Actions will automatically:
   - Build the project
   - Create a Velopack package
   - Publish it to GitHub Releases
4. Users will automatically receive the update within 4 hours

### Method 2: Manual Trigger via GitHub Actions

1. Go to your GitHub repository
2. Click on "Actions" tab
3. Select "Build and Release" workflow
4. Click "Run workflow"
5. Enter the version number (e.g., 1.0.0)
6. Click "Run workflow"

### Version Numbering

Use semantic versioning:
- **Major.Minor.Patch** (e.g., 1.0.0)
- Increment **Patch** for bug fixes (1.0.0 → 1.0.1)
- Increment **Minor** for new features (1.0.0 → 1.1.0)
- Increment **Major** for breaking changes (1.0.0 → 2.0.0)

## Testing the Update System

1. **Create first release**: Push tag `v1.0.0`
2. **Wait for GitHub Actions** to complete
3. **Download and install** the release from GitHub
4. **Make a change** to your code
5. **Create new release**: Push tag `v1.1.0`
6. **Wait for GitHub Actions** to complete
7. **Wait up to 4 hours** or check logs to see update detection
8. **Restart the app** to apply the update

## Monitoring Updates

Check the application logs to see update activity:
- "Checking for updates..."
- "New version available: X.X.X"
- "Downloading update..."
- "Update downloaded successfully"

## Advanced Configuration

### Change Update Check Interval

In `Services/UpdateService.cs`, modify:
```csharp
private readonly TimeSpan _checkInterval = TimeSpan.FromHours(4);
```

### Enable Auto-Restart After Update

In `Services/UpdateService.cs`, uncomment this line:
```csharp
// _updateManager.ApplyUpdatesAndRestart(newVersion);
```

**Warning**: This will restart the app without user confirmation.

### Add Update Notification to UI

You can add a notification icon or message box in Form1 by creating a public event in UpdateService:
```csharp
public event EventHandler<string>? UpdateAvailable;
```

## Workflow Details

The GitHub Actions workflow (`.github/workflows/release.yml`):
- Triggers on version tags (v*.*.*)
- Can be manually triggered with version input
- Builds in Release configuration
- Creates Velopack installer package
- Publishes to GitHub Releases automatically
- Includes release notes

## Troubleshooting

### Updates Not Detected
- Check that releases are public on GitHub
- Verify the GitHub repository URL in UpdateService.cs
- Check application logs for errors

### Build Fails in GitHub Actions
- Ensure all dependencies are properly referenced
- Check that .NET 8 SDK is available
- Verify vpk tool installs correctly

### Users Don't Receive Updates
- Ensure they have the installed version (not running from build folder)
- Check that update service is running (check logs)
- Verify internet connectivity
- Confirm GitHub Releases are public
