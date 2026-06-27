"""COCO label normalization for word chain game."""


def make_normalizer(label_map: dict[str, str], exclude: set[str]):
    """raw COCO label → game-friendly lowercase word, or None if excluded."""
    def normalize(raw: str) -> str | None:
        raw = raw.lower().strip()
        if raw in exclude:
            return None
        return label_map.get(raw, raw.replace(" ", ""))
    return normalize
