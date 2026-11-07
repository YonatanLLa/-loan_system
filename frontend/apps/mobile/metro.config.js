// Metro configurado para el monorepo pnpm de `prestamos-frontend`.
// Permite que la app móvil consuma los paquetes del workspace (p. ej.
// `@prestamos/api-client`) resolviendo su código fuente TypeScript.
// Ref: https://docs.expo.dev/guides/monorepos/
const { getDefaultConfig } = require('expo/metro-config');
const path = require('path');

const projectRoot = __dirname;
// apps/mobile -> apps -> raíz del frontend (donde vive pnpm-workspace.yaml)
const workspaceRoot = path.resolve(projectRoot, '../..');

const config = getDefaultConfig(projectRoot);

// 1. Vigilar toda la carpeta del monorepo para detectar cambios en packages/*.
config.watchFolders = [workspaceRoot];

// 2. Buscar módulos tanto en la app como en la raíz del workspace.
config.resolver.nodeModulesPaths = [
  path.resolve(projectRoot, 'node_modules'),
  path.resolve(workspaceRoot, 'node_modules'),
];

// 3. pnpm usa enlaces simbólicos (store aislado): Metro debe seguirlos.
config.resolver.unstable_enableSymlinks = true;
config.resolver.unstable_enablePackageExports = true;

module.exports = config;
