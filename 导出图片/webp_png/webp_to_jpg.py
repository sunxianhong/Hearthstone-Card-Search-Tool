import os
from concurrent.futures import ThreadPoolExecutor

from PIL import Image


def convert_webp_to_jpg(input_path, output_path, quality=95):
    """Convert one WEBP image to JPG."""
    try:
        with Image.open(input_path) as img:
            if img.mode in ("RGBA", "LA") or (
                img.mode == "P" and "transparency" in img.info
            ):
                rgba = img.convert("RGBA")
                background = Image.new("RGBA", rgba.size, (255, 255, 255, 255))
                background.alpha_composite(rgba)
                jpg_img = background.convert("RGB")
            else:
                jpg_img = img.convert("RGB")

            jpg_img.save(output_path, "JPEG", quality=quality, optimize=True)

        print(
            f"Success: {os.path.basename(os.path.dirname(output_path))}/"
            f"{os.path.basename(output_path)}"
        )
    except Exception as exc:
        print(f"Failed {input_path}: {exc}")


def batch_convert_nested(input_dir, output_dir, max_workers=8, quality=95):
    """Batch convert nested WEBP files while keeping the folder structure."""
    tasks = []

    for root, dirs, files in os.walk(input_dir):
        for filename in files:
            if filename.lower().endswith(".webp"):
                input_path = os.path.join(root, filename)
                rel_dir = os.path.relpath(root, input_dir)
                target_dir = os.path.join(output_dir, rel_dir)
                os.makedirs(target_dir, exist_ok=True)

                output_filename = os.path.splitext(filename)[0] + ".jpg"
                output_path = os.path.join(target_dir, output_filename)
                tasks.append((input_path, output_path))

    total_images = len(tasks)
    if total_images == 0:
        print("No WEBP images found.")
        return

    print(
        f"Found {total_images} WEBP images. "
        "Keeping folder structure and converting..."
    )

    with ThreadPoolExecutor(max_workers=max_workers) as executor:
        for input_path, output_path in tasks:
            executor.submit(convert_webp_to_jpg, input_path, output_path, quality)

    print("All images processed.")


def find_image_base_dir():
    """Find the folder that contains both cardpng and webp_images."""
    script_dir = os.path.dirname(os.path.abspath(__file__))
    candidates = []

    for start_dir in (script_dir, os.getcwd()):
        current_dir = os.path.abspath(start_dir)
        while current_dir not in candidates:
            candidates.append(current_dir)
            parent_dir = os.path.dirname(current_dir)
            if parent_dir == current_dir:
                break
            current_dir = parent_dir

    for candidate in candidates:
        cardpng_dir = os.path.join(candidate, "cardpng")
        webp_dir = os.path.join(candidate, "webp_images")
        if os.path.isdir(cardpng_dir) and os.path.isdir(webp_dir):
            return candidate

    raise SystemExit("Could not find cardpng and webp_images folders.")


if __name__ == "__main__":
    base_dir = find_image_base_dir()

    INPUT_FOLDER = os.path.join(base_dir, "webp_images")
    OUTPUT_FOLDER = os.path.join(base_dir, "cardpng")

    if not os.path.isdir(INPUT_FOLDER):
        raise SystemExit(f"Input folder not found: {INPUT_FOLDER}")

    os.makedirs(OUTPUT_FOLDER, exist_ok=True)

    batch_convert_nested(INPUT_FOLDER, OUTPUT_FOLDER)
