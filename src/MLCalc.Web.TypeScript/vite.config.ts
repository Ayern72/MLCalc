/// <reference types="vitest/config" />
import { execSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';

const repository = fileURLToPath(new URL('../..', import.meta.url));

/**
 * The build's version, made the way the .NET build makes it, so that the page can compare it with
 * the server's (Welcome.version): VersionPrefix from Directory.Build.props, then "+" and the commit.
 * The Docker build passes the commit as SOURCE_REVISION; a build from a clone asks git.
 */
function buildVersion(): string {
	const props = readFileSync(`${repository}/Directory.Build.props`, 'utf8');
	const prefix = /<VersionPrefix>([^<]+)<\/VersionPrefix>/.exec(props)?.[1] ?? '0.0.0';
	let revision = process.env['SOURCE_REVISION'] ?? '';
	if (revision === '') {
		try {
			revision = execSync('git rev-parse HEAD', { cwd: repository, stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim();
		} catch {
			// Not a clone: no commit, as in the .NET build.
		}
	}
	return revision === '' ? prefix : `${prefix}+${revision}`;
}

export default defineConfig({
	// The server serves the client at /ts/ (src/MLCalc.Web.Server/TypeScriptClient.cs).
	base: '/ts/',
	define: {
		__MLCALC_VERSION__: JSON.stringify(buildVersion()),
	},
	build: {
		// Next to the server, which serves the folder (WebOptions.TypeScriptClient).
		outDir: '../MLCalc.Web.Server/ts-client',
		emptyOutDir: true,
	},
	server: {
		port: 5173,
		// The stylesheet and icon are the Blazor client's; the tests read tests/fixtures.
		fs: { allow: ['../..'] },
		// npm run dev: the page from Vite, the hub from the server (dotnet run, port 5080).
		proxy: {
			'/hub': { target: 'http://localhost:5080', ws: true },
		},
	},
	test: {
		include: ['test/**/*.test.ts'],
	},
});
