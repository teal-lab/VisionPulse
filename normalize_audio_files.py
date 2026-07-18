import os

from pydub import AudioSegment
from pydub.effects import normalize


def main() -> None:
    source_directory: str = r""
    output_directory: str = r""

    os.makedirs(output_directory, exist_ok=True)

    valid_audio_extensions: set[str] = {"mp3", "wav"}

    for filename in os.listdir(source_directory):
        audio_file_extension: str = filename.lower()[-3:]

        if audio_file_extension not in valid_audio_extensions:
            print(f"Skipping file: {filename}")
            continue

        if filename.lower().endswith(audio_file_extension):
            file_path: str = os.path.join(source_directory, filename)

            audio: AudioSegment = AudioSegment.from_mp3(file_path)
            normalized_audio: AudioSegment = normalize(audio)

            normalized_file_path: str = os.path.join(output_directory, filename)
            normalized_audio.export(normalized_file_path, format=audio_file_extension)

            print(f"Saved normalized file as: {normalized_file_path}")

    print(f"All files have been normalized and saved in: {output_directory}")


if __name__ == "__main__":
    main()
