import { readFileSync } from 'node:fs';
import { defineConfig, type Plugin } from 'vite';

// The packages bundled into the script. Each is BSD-3-Clause, which asks that its notice travels with every copy, so
// the notices are written beside the script and the script says where they are.
const bundled = ['lit', 'lit-html', 'lit-element', '@lit/reactive-element'];

const notices = (): Plugin => ({
  name: 'third-party-notices',
  generateBundle() {
    const text = bundled
      .map((name) => {
        const { version, license } = JSON.parse(readFileSync(`node_modules/${name}/package.json`, 'utf8'));
        return `${name} ${version} (${license})\n\n${readFileSync(`node_modules/${name}/LICENSE`, 'utf8').trim()}`;
      })
      .join('\n\n----------------------------------------\n\n');
    this.emitFile({
      type: 'asset',
      fileName: 'THIRD-PARTY-NOTICES.txt',
      source: `The Heliograph workbench script includes the following software.\n\n${text}\n`,
    });
  },
});

// One self-contained module, so any host can load it with a single <script type="module">: the standalone page
// in Heliograph.Workbench, and the Umbraco backoffice in Heliograph.Umbraco.
export default defineConfig({
  plugins: [notices()],
  build: {
    outDir: '../Heliograph.Workbench/wwwroot',
    emptyOutDir: true,
    sourcemap: true,
    lib: {
      entry: 'src/index.ts',
      formats: ['es'],
      fileName: () => 'heliograph-workbench.js',
    },
    rollupOptions: {
      output: {
        banner: '/*! Heliograph workbench. Includes Lit (BSD-3-Clause): see THIRD-PARTY-NOTICES.txt beside this file. */',
      },
    },
  },
});
