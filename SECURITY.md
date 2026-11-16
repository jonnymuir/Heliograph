# Security policy

## Reporting a vulnerability

Please report vulnerabilities privately through
[GitHub's private vulnerability reporting](https://github.com/jonnymuir/Heliograph/security/advisories/new),
not in a public issue or pull request. Include the version, what an attacker can do, and the steps
to reproduce it.

This is a solo-maintained project, so reports are answered as soon as possible rather than against
a fixed timescale. Fixes are credited in the advisory unless you'd rather not be named.

## Supported versions

Only the latest release receives security fixes.

## Scope

In scope: the published Heliograph packages, in particular anything that lets merged data inject
markup or content into rendered output, leaks one recipient's data into another's correspondence,
or forces unbounded work during a render.

Out of scope: how a host delivers rendered output (its providers, queues and storage), and any
local sample or demo host that is never published.
