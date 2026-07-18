import os
from typing import cast

import pyttsx3
from pyttsx3.engine import Engine
from pyttsx3.voice import Voice


def main() -> None:
    output_directory: str = r""
    folder_names: list[str] = ["1x Speed", "1.25x Speed", "1.5x Speed", "2x Speed", "4x Speed"]
    speeds: list[float] = [1.0, 1.25, 1.5, 2.0, 4.0]
    filenames: list[str] = []
    texts: list[str] = []
    audio_file_extension: str = "wav"

    if not output_directory:
        exit(1)

    if len(folder_names) != len(speeds):
        exit(1)

    if len(filenames) != 0 and len(filenames) != len(texts):
        exit(1)

    os.makedirs(output_directory, exist_ok=True)

    engine: Engine = pyttsx3.init()

    default_rate: int = cast(int, engine.getProperty("rate"))

    voices: list[Voice] = cast(list[Voice], engine.getProperty("voices"))
    engine.setProperty("voice", voices[1].id)

    for folder_name, speed in zip(folder_names, speeds):
        folder_path: str = os.path.join(output_directory, folder_name)
        os.makedirs(folder_path, exist_ok=True)

        engine.setProperty("rate", int(default_rate * speed))

        for i in range(0, len(texts)):
            filename: str = f"{filenames[i]}.{audio_file_extension}" if len(filenames) != 0 else f"{texts[i]}.{audio_file_extension}"
            file_path: str = os.path.join(folder_path, filename)

            engine.save_to_file(texts[i], file_path)

    engine.runAndWait()
    engine.stop()


def get_filenames_from_directory(dir_path: str) -> list[str]:
    return [
        os.path.splitext(f)[0]
        for f in os.listdir(dir_path)
        if os.path.isfile(os.path.join(dir_path, f)) and not f.endswith('.meta')
    ]


def get_filenames_and_text_from_txt(path_to_txt: str) -> tuple[list[str], list[str]]:
    filenames: list[str] = []
    texts: list[str] = []

    with open(path_to_txt, "r") as f:
        for line in f:
            line = line.strip()

            if not line or line.startswith("#"):
                continue

            if line.startswith("* "):
                line = line.removeprefix("* ")

            name, text = line.split(":", 1)
            filenames.append(name.strip())
            texts.append(text.strip())

    return filenames, texts


if __name__ == "__main__":
    main()
