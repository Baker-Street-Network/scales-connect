# Baker Scale Connect

## Making a release

Use the GitHub Actions workflow named `Build and Release`.

### Steps

1. Push the changes you want included in the release.
2. Open the repo on GitHub.
3. Go to `Actions`.
4. Open `Build and Release`.
5. Click `Run workflow`.
6. Enter the version number, for example `1.0.3`.
7. Run the workflow.

### What it does

The workflow builds the app, creates the Velopack package, and publishes a GitHub Release with the generated files attached.

It can also run automatically if you push a tag like `v1.0.3`.
