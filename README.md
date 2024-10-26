![image](https://github.com/microsoft/devhomeazureextension/blob/main/src/AzureExtensionServer/Assets/StoreDisplay-150.png)

# Welcome to the Dev Home Azure Extension repo

This repository contains the source code for:

* [Dev Home Azure Extension](https://aka.ms/devhomeazureextension)
* Dev Home Azure widgets

Related repositories include:

* [Dev Home](https://github.com/microsoft/devhome)

## Installing and running Dev Home Azure Extension

> **Note**: The Dev Home Azure Extension requires Dev Home. Dev Home requires Windows 11 version 21H2 (build 22000) or Windows 10 version 22H2 (build 19045) or newer.

### Dev Home Extensions library [Recommended]
You can install the Dev Home Azure Extension from the Extensions library within Dev Home. This page can be found in the left-hand navigation pane. The list of available extensions will provide links to install from the store.

### Microsoft Store

You can also install the Dev Home Azure Extension directly from its [Microsoft Store listing](https://aka.ms/devhomeazureextension).

### Other install methods

#### Via GitHub

For users who are unable to install the Dev Home Azure Extension from the Microsoft Store, released builds can be manually downloaded from this repository's [Releases page](https://github.com/microsoft/devhomeazureextension/releases).

---

## Dev Home Azure Extension overview

Please take a few minutes to review the overview below before diving into the code:

### Widgets

The Dev Home Azure Extension provides widgets for Dev Home's dashboard, which is built as a Windows widget renderer. These widgets are built using the [Windows widget platform](https://learn.microsoft.com/windows/apps/design/widgets/), which relies on [Adaptive Cards](https://learn.microsoft.com/windows/apps/design/widgets/widgets-create-a-template).

### Machine configuration repository recommendations

The machine configuration tool utilizes the Dev Home Azure Extension to recommend repositories to clone, but isn't required to clone and install apps. The app installation tool is powered by [winget](https://learn.microsoft.com/windows/package-manager/winget).

---

## Documentation

Documentation for the Dev Home Azure Extension can be found at https://aka.ms/devhomedocs.

---

## Contributing

We are excited to work alongside you, our amazing community, to build and enhance the Dev Home Azure Extension!

***BEFORE you start work on a feature/fix***, please read & follow our [Contributor's Guide](https://github.com/microsoft/devhomeazureextension/blob/main/CONTRIBUTING.md) to help avoid any wasted or duplicate effort.

## Communicating with the team

The easiest way to communicate with the team is via GitHub issues.

Please file new issues, feature requests and suggestions, but **DO search for similar open/closed preexisting issues before creating a new issue.**

If you would like to ask a question that you feel doesn't warrant an issue (yet), please reach out to us via Twitter:

* Kayla Cinnamon, Senior Product Manager: [@cinnamon_msft](https://twitter.com/cinnamon_msft)
* Clint Rutkas, Principal Product Manager: [@clintrutkas](https://twitter.com/clintrutkas)

## Building the code

* Clone the repository
* Uninstall the Preview version of the Dev Home Azure Extension (Dev Home has a hard time choosing which extension to use if two versions exist)
* Open `DevHomeAzureExtension.sln` in Visual Studio 2022 or later and build from the IDE, or run `build\scripts\build.ps1` from a Visual Studio command prompt.

## Code of conduct

We welcome contributions and suggestions. Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.
