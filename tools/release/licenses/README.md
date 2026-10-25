# License templates

Canonical [SPDX](https://github.com/spdx/license-list-data) license texts, keyed by SPDX identifier.
Copyright lines are removed because the notices print each package's own copyright above the text.

They are only used for dependencies that declare a license but ship no license file. A release
build fails if a bundled dependency needs a template that is missing here; add it from
`https://raw.githubusercontent.com/spdx/license-list-data/main/text/<SPDX-ID>.txt`.

`packages/<package-id>.txt` holds a dependency's exact upstream license when the package ships
none and the generic template would be inaccurate (for example Npgsql's PostgreSQL-style
license names Npgsql instead of the University of California). Overrides beat templates;
license files shipped inside a package beat both.
