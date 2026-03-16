# Velopack Quick Reference

## 🎯 Quick Start

1. **Add this line to Program.cs** (after line 33):
   ```csharp
   services.AddHostedService<UpdateService>();
   ```

2. **Create your first release**:
   ```powershell
   git tag v1.0.0
   git push origin v1.0.0
   ```

3. **Done!** GitHub Actions will build and publish automatically.

## 📦 Creating Releases

### Automatic (Recommended)
```powershell
git tag v1.0.1
git push origin v1.0.1
```

### Manual via GitHub
1. Go to GitHub → Actions → "Build and Release"
2. Click "Run workflow"
3. Enter version number
4. Click "Run workflow"

## 🔄 How Updates Work

- ✅ UpdateService checks every 4 hours
- ✅ Downloads updates automatically from GitHub
- ✅ Updates apply on next restart
- ✅ Logs all activity to console/debug

## 📊 Monitoring

Check logs for:
- "Checking for updates..."
- "New version available: X.X.X"
- "Downloading update..."
- "Update downloaded successfully"

## 🧪 Testing

1. Release v1.0.0
2. Install from GitHub Releases
3. Release v1.0.1
4. Wait max 4 hours (or check logs)
5. Restart app → update applied!

## 🔧 Configuration

**Change check interval** (Services/UpdateService.cs):
```csharp
private readonly TimeSpan _checkInterval = TimeSpan.FromHours(4);
```

**Enable auto-restart** (Services/UpdateService.cs):
```csharp
// Uncomment this line:
_updateManager.ApplyUpdatesAndRestart(newVersion);
```

## 📁 Files Added/Modified

- ✅ `BakerScaleConnect.csproj` - Velopack package added
- ✅ `Program.cs` - VelopackApp.Build().Run() added
- ⚠️ `Program.cs` - MANUAL: Add UpdateService line
- ✅ `Services/UpdateService.cs` - Background update checker
- ✅ `.github/workflows/release.yml` - Auto-build workflow

## 🚀 Release Checklist

- [ ] Add UpdateService line to Program.cs
- [ ] Commit changes
- [ ] Create version tag (v1.0.0)
- [ ] Push tag to GitHub
- [ ] Wait for GitHub Actions to complete
- [ ] Verify release appears in GitHub Releases
- [ ] Download and test installer

## 🆘 Troubleshooting

**Updates not detected?**
- Check logs for errors
- Verify GitHub Releases are public
- Confirm internet connectivity

**GitHub Actions failing?**
- Check Actions tab for error details
- Ensure .NET 8 SDK is available
- Verify vpk tool installation

**File locked errors?**
- Close Visual Studio
- Stop running application
- Rebuild

## 📚 Full Documentation

See `VELOPACK_SETUP.md` for complete details.
