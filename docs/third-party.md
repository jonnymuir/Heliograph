# Other people's software

Heliograph is MIT licensed. This page lists every piece of software it touches that is not, how
it is touched, and why that leaves the licence alone. A test fails if a package that ships gains
a dependency that is not listed here.

## What ships

Packages a shipped project references. The core library and the PDF converter reference none.

| Package | Licence | Used by |
|---|---|---|
| Lit (`lit`, `lit-html`, `lit-element`, `@lit/reactive-element`) | BSD 3-Clause | `Heliograph.Workbench`. Bundled into the workbench script, so it is distributed. BSD 3-Clause asks that its notice travels with it: the build writes `THIRD-PARTY-NOTICES.txt` beside the script, and the script's first line points to it. |
| `ModelContextProtocol.AspNetCore` | Apache 2.0 | `Heliograph.Mcp`. A NuGet dependency, restored by the host; nothing of it is copied into this repo. Apache 2.0 is permissive and sits under MIT code without changing its licence. |

## Fonts that ship

| Fonts | Licence | Used by |
|---|---|---|
| Liberation Sans, Serif and Mono 2.1.5 ([liberation-fonts](https://github.com/liberationfonts/liberation-fonts), release `liberation-fonts-ttf-2.1.5.tar.gz`, SHA-256 `7191c669…25d0`) | SIL Open Font License 1.1 | `Heliograph.Pdf`, inside the assembly. The OFL allows fonts to be bundled with software, on condition that the licence goes with them (`LICENSE-Liberation.txt` is in the package) and the fonts are not sold on their own. Subsets embedded in a PDF are allowed. The fonts are unmodified. |
| Carlito 1.103, the look-alike for Calibri (Google's `crosextrafonts-carlito-20130920.tar.gz` from the ChromeOS mirror, SHA-256 `4bd12b6c…af5a`; the same files LibreOffice ships) | SIL Open Font License 1.1 | `Heliograph.Pdf`, inside the assembly, on the same terms as Liberation (`LICENSE-Carlito.txt` is in the package). The fonts are unmodified. |
| Caladea 1.002, the look-alike for Cambria (Google's `crosextrafonts-20130214.tar.gz` from the ChromeOS mirror, SHA-256 `c48d1c2f…4d09`; the same files LibreOffice ships) | Apache License 2.0 | `Heliograph.Pdf`, inside the assembly. Apache 2.0 allows redistribution with the licence and copyright notice, which `LICENSE-Caladea.txt` in the package carries. The fonts are unmodified. Later releases of Caladea are under the OFL but changed its widths, so they no longer match Cambria. |

## What is run, and never shipped

| Software | Licence | How Heliograph touches it |
|---|---|---|
| [LibreOffice](https://www.libreoffice.org/) | MPL 2.0 | `Heliograph.Pdf.LibreOffice` starts a LibreOffice the host has installed, as a separate program, and reads the file it writes. No LibreOffice code or binary is in any Heliograph package. The tests drive it the same way. |
| [veraPDF](https://verapdf.org/) | GPL 3.0 or later, or MPL 2.0 or later, at the user's choice | Tests only. The test suite runs the official `verapdf/cli` container image and reads what it prints. Nothing that ships refers to it. |
| [Open XML SDK](https://github.com/dotnet/Open-XML-SDK) | MIT | Tests only, as a validator of rendered files. |
| [Poppler](https://poppler.freedesktop.org/) (`pdftotext`) | GPL 2.0 or 3.0 | Tests only. The tests run the `pdftotext` program the machine has installed, to read where each line of a PDF sits, and compare the two converters. Nothing that ships refers to it. |

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
