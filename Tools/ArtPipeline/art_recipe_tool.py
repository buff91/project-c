#!/usr/bin/env python3
"""Validate and version Project-C art recipe YAML files safely."""

from __future__ import annotations

import argparse
import copy
import json
import re
import sys
from pathlib import Path
from typing import Any

import yaml

from art_review import (
    DEFAULT_RECIPE_DIR,
    Recipe,
    RecipeRegistry,
    ReviewError,
)


ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9-]*$")


def parse_assignment(text: str) -> tuple[list[str], Any]:
    path, separator, raw_value = text.partition("=")
    keys = [key for key in path.split(".") if key]
    if not separator or not keys:
        raise ReviewError(
            f"Invalid assignment {text!r}; expected dotted.path=JSON"
        )
    try:
        value = json.loads(raw_value)
    except json.JSONDecodeError:
        value = raw_value
    return keys, value


def set_nested(document: dict[str, Any], keys: list[str], value: Any) -> None:
    current: dict[str, Any] = document
    for key in keys[:-1]:
        child = current.get(key)
        if not isinstance(child, dict):
            child = {}
            current[key] = child
        current = child
    current[keys[-1]] = value


def command_validate(args: argparse.Namespace) -> None:
    recipes = RecipeRegistry(args.recipe_dir).load_all()
    for recipe in recipes.values():
        recipe.validate_files()
    print(f"validated {len(recipes)} recipes")


def command_clone(args: argparse.Namespace) -> None:
    if not ID_PATTERN.fullmatch(args.new_id):
        raise ReviewError("new_id must be lowercase kebab-case")
    registry = RecipeRegistry(args.recipe_dir)
    source = registry.get(args.source_id)
    document = copy.deepcopy(source.document)
    document["id"] = args.new_id
    document["name"] = args.name or f"{source.name} ({args.new_id})"
    for assignment in args.set:
        keys, value = parse_assignment(assignment)
        if keys[0] in {"schema_version", "id"}:
            raise ReviewError(
                f"{'.'.join(keys)} cannot be overridden with --set"
            )
        set_nested(document, keys, value)
    destination = args.recipe_dir.resolve() / f"{args.new_id}.yaml"
    if destination.exists():
        raise ReviewError(f"Refusing to overwrite recipe {destination}")
    validated = Recipe.from_document(document, path=destination)
    validated.validate_files()
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(
        yaml.safe_dump(
            document,
            allow_unicode=True,
            sort_keys=False,
            width=100,
        ),
        encoding="utf-8",
    )
    print(destination)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--recipe-dir",
        type=Path,
        default=DEFAULT_RECIPE_DIR,
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    validate = subparsers.add_parser("validate")
    validate.set_defaults(handler=command_validate)

    clone = subparsers.add_parser("clone")
    clone.add_argument("source_id")
    clone.add_argument("new_id")
    clone.add_argument("--name")
    clone.add_argument(
        "--set",
        action="append",
        default=[],
        metavar="DOTTED.PATH=JSON",
    )
    clone.set_defaults(handler=command_clone)
    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    try:
        args.recipe_dir = args.recipe_dir.expanduser().resolve()
        args.handler(args)
    except (ReviewError, OSError, ValueError, yaml.YAMLError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
