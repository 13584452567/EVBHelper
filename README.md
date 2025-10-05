# EVBHelper

EVBHelper is an Avalonia-based desktop utility that helps manage Allwinner evaluation boards. It offers a graphical workflow to flash firmware images through the `rfel` command-line tool and to run common FEL maintenance operations.

## Highlights

- Configure the `rfel` executable path and choose the desired logging verbosity (`-v`, `-vv`, `-vvv`).
- Browse for firmware images, provide the load address, and optionally initialize DDR or reset the device after flashing.
- Run device checks, start flashing, trigger resets, or cancel long-running actions without leaving the GUI.
- Review a structured log pane with timestamped entries and quick status messages.

## Prerequisites

- .NET SDK 10 (or newer with compatible target frameworks).
- The [`rfel`](https://github.com/) command-line utility installed locally or available on `PATH`.
- Windows, Linux, or macOS desktop environment.

## Getting Started

1. Clone the repository:

    ```bash
    git clone <repo-url>
    cd EVBHelper
    ```

2. Build the solution:

    ```bash
    dotnet build
    ```

3. Launch the desktop app:

    ```bash
    dotnet run --project EVBHelper/EVBHelper.csproj
    ```

4. Execute tests:

    ```bash
    dotnet test
    ```

## Usage Tips

1. Provide the path to `rfel` (or simply type `rfel` if the executable is on `PATH`).
2. Select the firmware file, confirm the load address (for example `0x40008000`), and enable optional steps like DDR initialization or automatic reset.
3. Click **Check Device** to confirm the board is in FEL mode.
4. Choose **Start Flashing** to write the image; monitor progress in the log view.
5. Use **Cancel** to stop the current operation or **Clear Log** to reset the output panel.

## Troubleshooting

- **`rfel` not found**: Confirm the path is correct or update your `PATH` environment variable.
- **Flash failures**: Review the log entries, verify the board is in FEL mode, and double-check the load address and firmware image.
- **DDR initialization issues**: Try changing the profile value or disable the option and retry.

Contributions are welcome¡ªfeel free to open issues or submit pull requests.
