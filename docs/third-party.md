# Other people's software

Heliograph is MIT licensed. This page lists every piece of software it touches that is not, how
it is touched, and why that leaves the licence alone. A test fails if a package that ships gains
a dependency that is not listed here.

## What ships

Packages a shipped project references. The core library and the PDF converter reference none.

| Package | Licence | Used by |
|---|---|---|
| `ModelContextProtocol.AspNetCore` | Apache 2.0 | `Heliograph.Mcp`. A NuGet dependency, restored by the host; nothing of it is copied into this repo. Apache 2.0 is permissive and sits under MIT code without changing its licence. |

## What is run, and never shipped

| Software | Licence | How Heliograph touches it |
|---|---|---|
| [LibreOffice](https://www.libreoffice.org/) | MPL 2.0 | `Heliograph.Pdf.LibreOffice` starts a LibreOffice the host has installed, as a separate program, and reads the file it writes. No LibreOffice code or binary is in any Heliograph package. The tests drive it the same way. |
| [veraPDF](https://verapdf.org/) | GPL 3.0 or later, or MPL 2.0 or later, at the user's choice | Tests only. The test suite runs the official `verapdf/cli` container image and reads what it prints. Nothing that ships refers to it. |
| [Open XML SDK](https://github.com/dotnet/Open-XML-SDK) | MIT | Tests only, as a validator of rendered files. |

## Why this is safe

A copyleft licence puts conditions on **distributing** the software it covers, or work built
from its source. Heliograph does neither:

- **Nothing is copied in.** No source file from LibreOffice or veraPDF is in this repository.
- **Nothing is linked.** No Heliograph package references their libraries. They run as separate
  programs and exchange files.
- **Nothing is passed on.** No Heliograph package contains their binaries. A host installs
  LibreOffice itself; the tests pull veraPDF's own image from its publisher.

Offering Heliograph over a network, through the API or an MCP server, changes none of this.
MPL 2.0 and GPL 3.0 have no condition triggered by network use (that is the AGPL, which neither
uses), and a tool that converts a letter still only starts the host's LibreOffice.

## What would change it

Each of these needs a decision first, and a row in this page:

- **Shipping a container image with LibreOffice inside**, for example for the reference app.
  That distributes LibreOffice. MPL 2.0 allows it; the image must say that LibreOffice is under
  MPL 2.0 and where its source is.
- **Putting veraPDF in anything that ships**, such as a "check this PDF" operation with veraPDF
  bundled. Take it under MPL 2.0, not the GPL, keep it a separate program, and say where its
  source is. Calling a veraPDF the host runs itself needs none of that.
- **Copying or changing one of their source files.** That file stays under its own licence.
- **Referencing a library under the GPL or AGPL from a package that ships.** Do not.

This is the maintainer's reading, not legal advice.
