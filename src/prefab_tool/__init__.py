"""Unity Prefab Deterministic Serializer.

A tool for canonical serialization of Unity YAML files (prefabs, scenes, assets)
to eliminate non-deterministic changes and reduce VCS noise.
"""

__version__ = "0.1.0"

from prefab_tool.git_utils import (
    UNITY_ANIMATION_EXTENSIONS,
    UNITY_AUDIO_EXTENSIONS,
    UNITY_CORE_EXTENSIONS,
    UNITY_EXTENSIONS,
    UNITY_PHYSICS_EXTENSIONS,
    UNITY_RENDERING_EXTENSIONS,
    UNITY_TERRAIN_EXTENSIONS,
    UNITY_UI_EXTENSIONS,
    get_changed_files,
    get_files_changed_since,
    get_repo_root,
    is_git_repository,
)
from prefab_tool.normalizer import UnityPrefabNormalizer
from prefab_tool.parser import UnityYAMLDocument, UnityYAMLObject

__all__ = [
    # Classes
    "UnityPrefabNormalizer",
    "UnityYAMLDocument",
    "UnityYAMLObject",
    # Functions
    "get_changed_files",
    "get_files_changed_since",
    "get_repo_root",
    "is_git_repository",
    # Extension sets
    "UNITY_EXTENSIONS",
    "UNITY_CORE_EXTENSIONS",
    "UNITY_ANIMATION_EXTENSIONS",
    "UNITY_RENDERING_EXTENSIONS",
    "UNITY_PHYSICS_EXTENSIONS",
    "UNITY_TERRAIN_EXTENSIONS",
    "UNITY_AUDIO_EXTENSIONS",
    "UNITY_UI_EXTENSIONS",
]
