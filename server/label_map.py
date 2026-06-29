"""OIV7 label normalization for word chain game."""


def make_normalizer(label_map: dict[str, str], exclude: set[str]):
    """raw OIV7 label → game-friendly lowercase word, or None if excluded.

    Multi-word labels default to their last word ("Coffee cup" → "cup").
    Exceptions are listed in config.yaml label_normalize.
    """
    def normalize(raw: str) -> str | None:
        raw = raw.lower().strip()
        if raw in exclude:
            return None
        if raw in label_map:
            return label_map[raw]
        words = raw.split()
        return words[-1] if len(words) > 1 else raw
    return normalize
