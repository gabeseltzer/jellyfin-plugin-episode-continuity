# Jellyfin Episode Continuity plugin

Scaffolding only so far. Spec to follow in `SPEC.md`.

## Development

Open in VS Code and reopen in the devcontainer. It starts a Jellyfin 10.11 server on
http://localhost:8097 with `media/` mounted as `/media` and `dist/plugins` as the plugin directory.

- `scripts/deploy.sh` builds the plugin, copies it into Jellyfin, and restarts the server.
- `dotnet test` runs the unit tests.
- `scripts/package.sh` produces a release zip and repository manifest.

The devcontainer shares its base image layers, feature layers, Jellyfin image, and NuGet package
volume with the sibling `jellyfin-data-flow-plugin` repo. See `.devcontainer/docker-compose.yml`.

## License

GPL-3.0-or-later.
