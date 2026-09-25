import { randomBytes } from 'node:crypto';
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import process from 'node:process';

const project = `ambev-e2e-${process.pid}`;
const docker = process.platform === 'win32' ? 'docker.exe' : 'docker';
const repositoryRoot = fileURLToPath(new URL('../../../', import.meta.url));
const adminEmail = 'e2e.admin@example.test';
const adminPassword = randomBytes(24).toString('base64url');
const databasePassword = randomBytes(24).toString('base64url');
const jwtSecret = randomBytes(48).toString('base64url');
const environment = {
  ...process.env,
  POSTGRES_PASSWORD: databasePassword,
  ConnectionStrings__DefaultConnection: `Host=database;Port=5432;Database=developer_evaluation_e2e;Username=e2e;Password=${databasePassword}`,
  Jwt__SecretKey: jwtSecret,
  DevelopmentAdmin__Email: adminEmail,
  DevelopmentAdmin__Password: adminPassword,
  E2E_ADMIN_EMAIL: adminEmail,
  E2E_ADMIN_PASSWORD: adminPassword,
  E2E_JWT_SECRET: jwtSecret,
  E2E_BASE_URL: 'http://127.0.0.1:4201',
  E2E_PROXY_CONFIG: 'e2e/proxy.conf.json',
};

let stopping = false;
let mappedDrive = '';
let projectDirectory = repositoryRoot;

if (process.platform === 'win32') {
  mappedDrive = ['Z:', 'Y:', 'X:', 'W:', 'V:'].find((drive) => !existsSync(`${drive}\\`)) ?? '';
  if (!mappedDrive) throw new Error('No drive letter is available for the isolated E2E build.');
  await run('subst.exe', [mappedDrive, repositoryRoot]);
  projectDirectory = `${mappedDrive}\\`;
}

const composeArguments = [
  'compose',
  '--project-directory',
  projectDirectory,
  '-p',
  project,
  '-f',
  `${projectDirectory}src/frontend/e2e/docker-compose.yml`,
];

try {
  await run(docker, [...composeArguments, 'up', '--build', '-d', '--wait']);
  const result = await run(process.execPath, ['node_modules/@playwright/test/cli.js', 'test'], {
    rejectOnError: false,
  });
  process.exitCode = result;
} finally {
  stopping = true;
  await run(docker, [...composeArguments, 'down', '--volumes'], {
    rejectOnError: false,
  });
  if (mappedDrive) {
    await run('subst.exe', [mappedDrive, '/D'], { rejectOnError: false });
  }
}

function run(command, args, options = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, { env: environment, stdio: 'inherit', shell: false });
    child.once('error', reject);
    child.once('exit', (code, signal) => {
      const exitCode = code ?? (signal ? 1 : 0);
      if (exitCode !== 0 && options.rejectOnError !== false && !stopping) {
        reject(new Error(`${command} exited with code ${exitCode}.`));
        return;
      }
      resolve(exitCode);
    });
  });
}
