import { copyFileSync, existsSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";

const projectRoot = resolve(import.meta.dirname, "..");
const sourceFile = resolve(projectRoot, "node_modules", "gsap", "dist", "gsap.min.js");
const targetFile = resolve(projectRoot, "wwwroot", "lib", "gsap", "gsap.min.js");

if (!existsSync(sourceFile)) {
    throw new Error(`GSAP dist file not found: ${sourceFile}`);
}

mkdirSync(dirname(targetFile), { recursive: true });
copyFileSync(sourceFile, targetFile);

console.log(`Copied GSAP to ${targetFile}`);
