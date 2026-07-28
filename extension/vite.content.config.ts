import { defineConfig } from 'vite';

// Second build pass: bundle the content script as a single self-contained IIFE
// (content scripts cannot use ES module imports).
export default defineConfig({
  publicDir: false,
  build: {
    outDir: 'dist',
    emptyOutDir: false,
    target: 'es2020',
    lib: {
      entry: 'src/content/content.ts',
      name: 'ClipYourselfContent',
      formats: ['iife'],
      fileName: () => 'content.js',
    },
  },
});
