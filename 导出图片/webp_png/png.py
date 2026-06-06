import os
from PIL import Image
from concurrent.futures import ThreadPoolExecutor

def convert_to_webp_lossless(input_path, output_path):
    """单张图片转换函数"""
    try:
        with Image.open(input_path) as img:
            # 同样保持 lossless=True 和 quality=100 无损高压缩率计算
            img.save(output_path, 'webp', lossless=False, quality=50)
        # 打印出带文件夹名字的路径，方便你观察进度
        print(f"成功: {os.path.basename(os.path.dirname(output_path))}/{os.path.basename(output_path)}")
    except Exception as e:
        print(f"失败 {input_path}: {e}")

def batch_convert_nested(input_dir, output_dir, max_workers=8):
    """支持多层文件夹的批量转换函数"""
    tasks = []

    # os.walk 会自动钻进 input_dir 里面的每一个子文件夹
    for root, dirs, files in os.walk(input_dir):
        for filename in files:
            if filename.lower().endswith('.png'):
                # 1. 拿到原图片的绝对路径
                input_path = os.path.join(root, filename)

                # 2. 计算当前文件在原目录里的“相对位置” (比如 "动漫图/日系")
                rel_dir = os.path.relpath(root, input_dir)
                
                # 3. 在输出目录里拼接出同样的文件夹路径
                target_dir = os.path.join(output_dir, rel_dir)

                # 4. 如果这个同名文件夹不存在，就自动创建它
                os.makedirs(target_dir, exist_ok=True)

                # 5. 替换图片后缀并生成最终的保存路径
                output_filename = os.path.splitext(filename)[0] + '.webp'
                output_path = os.path.join(target_dir, output_filename)

                # 加入待处理任务列表
                tasks.append((input_path, output_path))

    total_images = len(tasks)
    if total_images == 0:
        print("未在目录或其子文件夹中找到 PNG 图片。")
        return

    print(f"共发现 {total_images} 张 PNG 图片，正在一比一克隆文件夹结构并转换...")
    
    # 多线程并发处理
    with ThreadPoolExecutor(max_workers=max_workers) as executor:
        for input_path, output_path in tasks:
            executor.submit(convert_to_webp_lossless, input_path, output_path)
            
    print("🎉 包含多层文件夹的所有图片处理完毕！")

if __name__ == "__main__":
    script_dir = os.path.dirname(os.path.abspath(__file__))

    INPUT_FOLDER = os.path.join(script_dir, "cardpng")
    OUTPUT_FOLDER = os.path.join(script_dir, "webp_images")

    if not os.path.isdir(INPUT_FOLDER):
        raise SystemExit(f"Input folder not found: {INPUT_FOLDER}")

    os.makedirs(OUTPUT_FOLDER, exist_ok=True)
    
    batch_convert_nested(INPUT_FOLDER, OUTPUT_FOLDER)
