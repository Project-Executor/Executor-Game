"""캐릭터 시트에서 배경을 지우고 포즈(덩어리)별 상자를 찾는다.
사용: python -I segment.py <시트.png> <출력 접두어> [밝기 기준]
배경 = 테두리에서 이어진 밝고 무채색인 픽셀 (체크무늬 배경도 포함). 안쪽의 흰 양말·눈은 테두리와 이어지지 않아 남는다."""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

src, out = sys.argv[1], sys.argv[2]
light = int(sys.argv[3]) if len(sys.argv) > 3 else 200
raw = Image.open(src)
transparent = raw.mode == 'RGBA' and (np.array(raw)[:, :, 3] == 0).mean() > 0.2
img = raw.convert('RGB')
if transparent:
    # 배경이 이미 투명한 시트: 반 이상 불투명한 픽셀만 캐릭터 (가장자리의 옅은 번짐은 버린다)
    fg = np.array(raw)[:, :, 3] >= 128
else:
    a = np.array(img).astype(int)
    mx, mn = a.max(axis=2), a.min(axis=2)
    bgish = (mn > light) & (mx - mn < 24)
    lab, n = ndimage.label(bgish)
    border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    bg = np.isin(lab, list(border))
    fg = ~bg
    # 가장자리 1px 정도 남는 밝은 테두리(안티에일리어싱)를 줄인다
    fg = ndimage.binary_opening(fg, iterations=1)
print('transparent sheet' if transparent else 'background removed by color')
alpha = (fg * 255).astype(np.uint8)
rgba = np.dstack([np.array(img), alpha])
Image.fromarray(rgba, 'RGBA').save(out + '_cut.png')

# 덩어리: 가까운 조각(머리카락 끝, 효과)을 묶으려고 넓혀서 라벨링
grown = ndimage.binary_dilation(fg, iterations=6)
lab2, n2 = ndimage.label(grown)
boxes = []
for i, sl in enumerate(ndimage.find_objects(lab2)):
    ys, xs = sl
    h, w = ys.stop - ys.start, xs.stop - xs.start
    if h * w < 2500:
        continue
    boxes.append((ys.start, xs.start, ys.stop, xs.stop))
boxes.sort(key=lambda b: (b[0] // 120, b[1]))
for b in boxes:
    print(b, 'w', b[3] - b[1], 'h', b[2] - b[0])
print('count', len(boxes))
