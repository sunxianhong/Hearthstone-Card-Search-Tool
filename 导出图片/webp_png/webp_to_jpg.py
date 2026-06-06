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


if __name__ == "__main__":
    script_dir = os.path.dirname(os.path.abspath(__file__))

    INPUT_FOLDER = os.path.join(script_dir, "webp_images")
    OUTPUT_FOLDER = os.path.join(script_dir, "jpg_images")

    batch_convert_nested(INPUT_FOLDER, OUTPUT_FOLDER)
