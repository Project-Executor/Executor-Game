"""
움직이는 도트(GIF)를 게임이 읽는 프레임 PNG로 나눈다. Unity는 GIF 애니메이션을 재생하지 못하고 첫 프레임만 쓰기 때문이다.

원본:  Art/player/{front|back|left|right}.gif        (게임에 들어가지 않는 원본 그림 폴더)
결과:  Assets/Resources/Pixel/player/{방향}_{번호}.png  (0부터. 0번이 멈춰 있을 때 자세)

사용 (Executor-Game 폴더에서):
    python Tools/gif_to_frames.py            # Art/player의 GIF를 모두 나눈다
    python Tools/gif_to_frames.py Art/npc     # 다른 폴더 -> Assets/Resources/Pixel/npc

필요: Pillow (pip install pillow)
"""
import glob
import os
import sys

from PIL import Image

src = sys.argv[1] if len(sys.argv) > 1 else 'Art/player'
dst = os.path.join('Assets/Resources/Pixel', os.path.basename(os.path.normpath(src)))
os.makedirs(dst, exist_ok=True)

gifs = sorted(glob.glob(os.path.join(src, '*.gif')))
if not gifs:
    sys.exit(f'{src}에 GIF가 없습니다.')

for gif in gifs:
    name = os.path.splitext(os.path.basename(gif))[0]
    # 예전에 만든 프레임은 지운다 (프레임 수가 줄었을 때 남지 않게)
    for old in glob.glob(os.path.join(dst, f'{name}_*.png')):
        os.remove(old)
    im = Image.open(gif)
    size = None
    for i in range(im.n_frames):
        im.seek(i)
        frame = im.convert('RGBA')  # 투명 배경 유지
        size = frame.size
        frame.save(os.path.join(dst, f'{name}_{i}.png'))
    print(f'{gif} -> {dst}/{name}_0..{im.n_frames - 1}.png ({im.n_frames}프레임, {size[0]}x{size[1]}, {im.info.get("duration")}ms)')
