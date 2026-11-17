import { dotnet } from './_framework/dotnet.js';

const AdapterRetryMs = [100, 200, 400, 800, 1600];
const PixelsPerNotch = 100;
const LinesPerNotch = 3;
const Alt = 1;
const Control = 2;
const Shift = 4;
const Gui = 8;
const Mac = /mac/i.test(navigator.platform);
const EditingLetters = new Set(['a', 'c', 'x', 'y', 'z']);

const canvas = document.getElementById('canvas');
const progress = document.getElementById('progress');

const show = sheet => {
  document.documentElement.dataset.page = sheet ?? 'ready';
  for (const each of document.querySelectorAll('.sheet'))
    each.hidden = each.id !== sheet;
};

const fail = cause => {
  console.error(cause);
  stopped = true;
  document.getElementById('failure').textContent = String(cause?.message ?? cause);
  show('failed');
};

let stopped = false;
// Held for the page's life: a collected adapter takes its device down with it.
let adapter = null;

const requestAdapter = async () => {
  for (let attempt = 0; ; attempt++) {
    const found = await navigator.gpu.requestAdapter({ powerPreference: 'high-performance' });
    if (found || attempt === AdapterRetryMs.length)
      return found;
    // A browser launched straight onto the page answers null until its GPU process is up.
    await new Promise(resolve => setTimeout(resolve, AdapterRetryMs[attempt]));
  }
};

const requestDevice = async () => {
  if (!navigator.gpu)
    return null;
  adapter = await requestAdapter();
  if (!adapter)
    return null;
  // bgfx uses whatever the device reports, and rejects timestamp-query's unset end-of-pass index.
  const requiredFeatures = [...adapter.features].filter(feature => feature !== 'timestamp-query');
  return adapter.requestDevice({ requiredFeatures }).catch(() => null);
};

const modifiers = e =>
  (e.altKey && !e.getModifierState('AltGraph') ? Alt : 0)
  | ((e.ctrlKey && !e.getModifierState('AltGraph')) || (Mac && e.metaKey) ? Control : 0)
  | (e.shiftKey ? Shift : 0)
  | (!Mac && e.metaKey ? Gui : 0);

const pixels = e => {
  const box = canvas.getBoundingClientRect();
  return [(e.clientX - box.left) * devicePixelRatio, (e.clientY - box.top) * devicePixelRatio];
};

const typed = e => {
  const held = modifiers(e);
  const inserts = (held & (Control | Gui)) === 0 && ((held & Alt) === 0 || Mac);
  return inserts && !e.isComposing && [...e.key].length === 1 ? e.key : '';
};

const keepsFromBrowser = e => {
  if (/^F\d+$/.test(e.code))
    return false;
  if (e.ctrlKey || e.metaKey)
    return EditingLetters.has(e.key.toLowerCase());
  return !e.altKey;
};

const isPaste = e => (e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'v';

const wireInput = page => {
  // A move carries the buttons it holds, so a release the page never heard still ends its press.
  addEventListener('mousemove', e => page.PointerMove(...pixels(e), e.buttons, modifiers(e)));
  canvas.addEventListener('mousedown', e =>
    page.PointerDown(...pixels(e), e.button, modifiers(e), Math.max(1, e.detail)));
  addEventListener('mouseup', e => page.PointerUp(...pixels(e), e.button, modifiers(e)));
  document.addEventListener('mouseleave', () => page.PointerLeave());
  canvas.addEventListener('contextmenu', e => e.preventDefault());
  canvas.addEventListener('wheel', e => {
    e.preventDefault();
    const notch = [PixelsPerNotch, LinesPerNotch, 1][e.deltaMode];
    page.Wheel(...pixels(e), e.deltaX / notch, -e.deltaY / notch, modifiers(e));
  }, { passive: false });

  const touch = (name, handler) => canvas.addEventListener(name, e => {
    if (e.pointerType !== 'touch')
      return;
    // Stops the browser following the touch with the mouse events a mouse would have sent.
    e.preventDefault();
    handler(e);
  });
  touch('pointerdown', e => {
    canvas.setPointerCapture(e.pointerId);
    page.TouchDown(e.pointerId, ...pixels(e), modifiers(e));
  });
  touch('pointermove', e => page.TouchMove(e.pointerId, ...pixels(e), modifiers(e)));
  touch('pointerup', e => page.TouchUp(e.pointerId, ...pixels(e), modifiers(e)));
  touch('pointercancel', e => page.TouchCancel(e.pointerId));

  addEventListener('keydown', e => {
    // Left to the browser, so it fires a paste event.
    if (isPaste(e))
      return;
    page.KeyDown(e.code, e.key, modifiers(e), e.repeat, typed(e));
    if (keepsFromBrowser(e))
      e.preventDefault();
  });
  addEventListener('keyup', e => {
    if (!isPaste(e))
      page.KeyUp(e.code, e.key, modifiers(e));
  });
  document.addEventListener('paste', e => {
    e.preventDefault();
    page.Paste(e.clipboardData?.getData('text') ?? '');
  });
  addEventListener('focus', () => page.Activate());
  addEventListener('blur', () => page.Deactivate());
};

const drawnSize = () => [
  Math.max(1, Math.round(canvas.clientWidth * devicePixelRatio)),
  Math.max(1, Math.round(canvas.clientHeight * devicePixelRatio)),
];

const run = page => {
  const frame = now => {
    if (stopped)
      return;
    try {
      const [width, height] = drawnSize();
      if (width !== canvas.width || height !== canvas.height) {
        canvas.width = width;
        canvas.height = height;
        page.Resize(width, height, devicePixelRatio);
      }
      page.Frame(now / 1000);
      requestAnimationFrame(frame);
    } catch (e) {
      fail(e);
    }
  };
  requestAnimationFrame(frame);
};

const start = async () => {
  const device = await requestDevice();
  if (!device) {
    show('unsupported');
    return;
  }
  device.lost.then(info => {
    if (info.reason !== 'destroyed')
      fail(`The graphics device was lost: ${info.message}`);
  });

  const runtime = await dotnet
    .withModuleConfig({
      preinitializedWebGPUDevice: device,
      onDownloadResourceProgress: (loaded, total) => {
        progress.textContent = `${loaded} of ${total}`;
      },
    })
    .create();
  runtime.Module.preinitializedWebGPUDevice = device;
  runtime.setModuleImports('main.js', {
    showStory: key => {
      const address = new URL(location.href);
      address.searchParams.set('story', key);
      history.replaceState(null, '', address);
    },
    setCursor: cursor => {
      canvas.style.cursor = cursor;
    },
    writeClipboard: text => {
      navigator.clipboard?.writeText(text).catch(console.warn);
    },
    textInput: on => {
      canvas.dataset.typing = on;
    },
  });

  const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
  const page = exports.Tralys.Ui.Gallery.Web.GalleryPage;
  const [width, height] = drawnSize();
  canvas.width = width;
  canvas.height = height;
  page.Start(width, height, devicePixelRatio, new URLSearchParams(location.search).get('story'));
  wireInput(page);
  show(null);
  run(page);
};

addEventListener('error', e => fail(e.error ?? e.message));
addEventListener('unhandledrejection', e => fail(e.reason));
start().catch(fail);
