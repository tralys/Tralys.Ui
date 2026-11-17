import os

# The release desktop FreeTypeSharp ships; a newer one rasterizes glyphs differently.
TAG = 'VER-2-13-2'
HASH = 'b66b7bc907b840e7900acd60c01d97aafd0cf101b294073ae729edf586b0ec9588d0914aa95d99cbf3b4146d308fc422a166c2aa501ac32cd33348f6774f485b'

LICENSE = 'FreeType License'


def get_lib_name(settings):
  if settings.SUPPORT_LONGJMP != 'wasm':
    return 'libtralys_freetype.a'
  return 'libtralys_freetype-legacysjlj.a' if settings.WASM_LEGACY_EXCEPTIONS else 'libtralys_freetype-sjlj.a'


def get(ports, settings, shared):
  ports.fetch_project('tralys_freetype', f'https://github.com/freetype/freetype/archive/{TAG}.zip', sha512hash=HASH)

  def create(final):
    source_path = ports.get_dir('tralys_freetype', f'freetype-{TAG}')
    ports.install_header_dir(os.path.join(source_path, 'include'), target='freetype2')

    srcs = ['builds/unix/ftsystem.c',
            'src/autofit/autofit.c',
            'src/base/ftbase.c',
            'src/base/ftbbox.c',
            'src/base/ftbdf.c',
            'src/base/ftbitmap.c',
            'src/base/ftcid.c',
            'src/base/ftdebug.c',
            'src/base/ftfstype.c',
            'src/base/ftgasp.c',
            'src/base/ftglyph.c',
            'src/base/ftgxval.c',
            'src/base/ftinit.c',
            'src/base/ftmm.c',
            'src/base/ftotval.c',
            'src/base/ftpatent.c',
            'src/base/ftpfr.c',
            'src/base/ftstroke.c',
            'src/base/ftsynth.c',
            'src/base/fttype1.c',
            'src/base/ftwinfnt.c',
            'src/bdf/bdf.c',
            'src/bzip2/ftbzip2.c',
            'src/cache/ftcache.c',
            'src/cff/cff.c',
            'src/cid/type1cid.c',
            'src/gzip/ftgzip.c',
            'src/lzw/ftlzw.c',
            'src/pcf/pcf.c',
            'src/pfr/pfr.c',
            'src/psaux/psaux.c',
            'src/pshinter/pshinter.c',
            'src/psnames/psnames.c',
            'src/raster/raster.c',
            'src/sdf/sdf.c',
            'src/sfnt/sfnt.c',
            'src/smooth/smooth.c',
            'src/svg/svg.c',
            'src/truetype/truetype.c',
            'src/type1/type1.c',
            'src/type42/type42.c',
            'src/winfonts/winfnt.c']

    flags = [
      '-DFT2_BUILD_LIBRARY',
      '-DHAVE_UNISTD_H',
      '-DHAVE_FCNTL_H',
      '-I' + source_path + '/include',
    ]

    # setjmp must lower to the link's exception model; emscripten's default is the legacy one.
    if settings.SUPPORT_LONGJMP == 'wasm':
      flags += ['-sSUPPORT_LONGJMP=wasm', f'-sWASM_LEGACY_EXCEPTIONS={int(settings.WASM_LEGACY_EXCEPTIONS)}']

    ports.build_port(source_path, final, 'tralys_freetype', flags=flags, srcs=srcs)

  return [shared.cache.get_lib(get_lib_name(settings), create, what='port')]


def clear(ports, settings, shared):
  shared.cache.erase_lib(get_lib_name(settings))


def process_args(ports):
  return ['-isystem', ports.get_include_dir('freetype2')]


def show():
  return f'tralys_freetype (--use-port=<path>/tralys_freetype.py; {LICENSE})'
