# Self-Hosted GitHub Actions Runner Setup

## Why Self-Hosted Runner?

Unity Personal licenses can't be activated programmatically in cloud CI runners. A self-hosted runner uses your machine where Unity is already activated.

## Setup Steps

### 1. Install GitHub Actions Runner on Your Machine

1. Go to: https://github.com/harmandeeppal/deterministic-rollback-toy/settings/actions/runners/new

2. Follow the displayed instructions, or use these commands in PowerShell:

```powershell
# Create a folder
cd C:\
mkdir actions-runner; cd actions-runner

# Download the latest runner package
Invoke-WebRequest -Uri https://github.com/actions/runner/releases/download/v2.321.0/actions-runner-win-x64-2.321.0.zip -OutFile actions-runner-win-x64-2.321.0.zip

# Extract the installer
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory("$PWD/actions-runner-win-x64-2.321.0.zip", "$PWD")
```

### 2. Configure the Runner

```powershell
# Get your token from https://github.com/harmandeeppal/deterministic-rollback-toy/settings/actions/runners/new
.\config.cmd --url https://github.com/harmandeeppal/deterministic-rollback-toy --token YOUR_TOKEN_HERE

# When prompted:
# - Runner group: Default
# - Runner name: (press Enter to use default, e.g., "DESKTOP-XYZ")
# - Labels: unity-windows (or press Enter for default)
# - Work folder: (press Enter for default)
```

### 3. Run the Runner

```powershell
# Start the runner (keep this terminal open)
.\run.cmd
```

**Or install as a Windows Service** (recommended for always-on):

```powershell
# Run as Administrator:
.\svc.cmd install
.\svc.cmd start
```

### 4. Update Workflow to Use Self-Hosted Runner

The workflow needs a small change to target your runner instead of `ubuntu-latest`.

**Current:**
```yaml
runs-on: ubuntu-latest
```

**Updated:**
```yaml
runs-on: self-hosted
```

## Workflow Changes Needed

Update `.github/workflows/unity-tests.yml`:

1. Change `runs-on: ubuntu-latest` to `runs-on: self-hosted`
2. Remove Unity activation steps (not needed - Unity already activated)
3. Use Windows paths if needed

## Verify Runner is Online

Check: https://github.com/harmandeeppal/deterministic-rollback-toy/settings/actions/runners

You should see your runner listed with a green dot (online).

## Pros/Cons

### Pros
✅ Works immediately with Personal license
✅ No license secrets needed
✅ Faster builds (no Docker overhead)
✅ Can test Windows-specific features

### Cons
❌ Machine must be running for CI to work
❌ Ties up your machine during builds
❌ Only tests on your OS/Unity version

## Alternative: Use Ubuntu VM

If you want a dedicated CI machine:
1. Set up an Ubuntu VM (VirtualBox/Hyper-V)
2. Install Unity on VM and activate Personal license
3. Install GitHub Actions runner on VM
4. Leave VM running 24/7

## Next Steps

1. Choose: self-hosted runner vs. cloud runner with Pro license
2. If self-hosted: follow steps above
3. Update workflow file
4. Test with a push to the branch

