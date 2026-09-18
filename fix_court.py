from PIL import Image
import os

img_path = r'Assets\BasketballBetting\Art\Textures\Court_Albedo.png'
out_path = r'Assets\BasketballBetting\Art\Textures\Court_Albedo.png'

img = Image.open(img_path).convert('RGBA')
w, h = img.size

# The bottom half is from y = h//2 to h
bottom_half = img.crop((0, h//2, w, h))

# To make the top half, we flip the bottom half vertically AND horizontally (so it matches the other side rotationally)
top_half = bottom_half.transpose(Image.FLIP_TOP_BOTTOM).transpose(Image.FLIP_LEFT_RIGHT)

# Create a new image
new_img = Image.new('RGBA', (w, h))
new_img.paste(top_half, (0, 0))
new_img.paste(bottom_half, (0, h//2))

# Now we need to restore the center logo!
# The center logo is a circle in the middle.
# Let's find the approximate radius of the center logo.
# It looks like the outer blue circle has a white border.
# We can just copy a square box from the center of the original image, applying a circular mask to blend it.
center_radius = int(w * 0.22) # guess based on image proportions
cx, cy = w // 2, h // 2

# We'll create a circular mask for the center area
mask = Image.new('L', (w, h), 0)
from PIL import ImageDraw
draw = ImageDraw.Draw(mask)
draw.ellipse((cx - center_radius, cy - center_radius, cx + center_radius, cy + center_radius), fill=255)

# Paste the original center over the new image using the mask
new_img.paste(img, (0, 0), mask)

new_img.save(out_path)
print('Fixed Court_Albedo.png successfully.')
