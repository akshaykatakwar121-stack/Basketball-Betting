using UnityEngine;

namespace BasketballBetting
{
    public static class PlaceholderCharacterBuilder
    {
        public static HumanoidRig Build(CharacterDefinition def, Transform parent, bool defenderPalette = false)
        {
            CharacterVisualProfile p = def != null ? def.Placeholder : DefaultProfile();
            if (p == null)
                p = DefaultProfile();

            var rootGo = new GameObject(def != null ? def.DisplayName : "Player");
            rootGo.transform.SetParent(parent, false);
            var rig = new HumanoidRig
            {
                Root = rootGo.transform,
                Definition = def,
                Height = p.height
            };

            if (TryBuildImported(def, p, rootGo.transform, rig))
                return rig;

            float h = p.height;
            float muscle = p.muscle;
            float bulk = p.bulk;
            Color jersey = defenderPalette ? new Color(0.92f, 0.92f, 0.94f) : p.jersey;
            Color shorts = defenderPalette ? new Color(0.08f, 0.08f, 0.1f) : p.shorts;
            Color trim = defenderPalette ? new Color(0.15f, 0.15f, 0.18f) : p.trim;

            rig.Hips = Pivot(rootGo.transform, "Hips", new Vector3(0f, h * 0.52f, 0f));
            MeshOn(rig.Hips, "HipsMesh", PrimitiveType.Cube, Vector3.zero, new Vector3(0.28f * bulk, 0.12f, 0.16f * bulk), p.skin);
            rig.Spine = Pivot(rig.Hips, "Spine", new Vector3(0f, 0.12f, 0f));
            MeshOn(rig.Spine, "SpineMesh", PrimitiveType.Cube, Vector3.zero, new Vector3(0.26f * bulk, 0.16f, 0.15f), jersey);
            rig.Chest = Pivot(rig.Spine, "Chest", new Vector3(0f, 0.18f * muscle, 0f));
            MeshOn(rig.Chest, "ChestMesh", PrimitiveType.Cube, Vector3.zero, new Vector3(0.38f * bulk, 0.28f * muscle, 0.20f * bulk), jersey);
            AddNumber(rig.Chest, def != null ? def.JerseyNumber : p.jerseyNumber, trim);

            rig.Head = Pivot(rig.Chest, "Head", new Vector3(0f, 0.28f, 0f));
            MeshOn(rig.Head, "HeadMesh", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.20f, 0.22f, 0.20f), p.skin);
            if (!p.bald)
            {
                Vector3 hairScale = p.longHair ? new Vector3(0.22f, 0.20f, 0.24f) : new Vector3(0.21f, 0.10f, 0.21f);
                Vector3 hairPos = p.longHair ? new Vector3(0f, 0.06f, -0.02f) : new Vector3(0f, 0.08f, 0f);
                MeshOn(rig.Head, "Hair", PrimitiveType.Sphere, hairPos, hairScale, p.hair);
            }
            if (p.headband)
                MeshOn(rig.Head, "Headband", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.22f, 0.02f, 0.22f), trim);

            rig.RightUpperArm = Limb(rig.Chest, "RightUpperArm", new Vector3(0.24f * bulk, 0.08f, 0f), new Vector3(0.09f * muscle, 0.22f, 0.09f), p.skin);
            rig.RightLowerArm = Limb(rig.RightUpperArm, "RightLowerArm", new Vector3(0f, -0.22f, 0f), new Vector3(0.075f, 0.20f, 0.075f), p.skin);
            rig.RightHand = Limb(rig.RightLowerArm, "RightHand", new Vector3(0f, -0.16f, 0.02f), new Vector3(0.07f, 0.08f, 0.08f), p.skin);
            rig.RightHand.name = "RightHand";

            rig.LeftUpperArm = Limb(rig.Chest, "LeftUpperArm", new Vector3(-0.24f * bulk, 0.08f, 0f), new Vector3(0.09f * muscle, 0.22f, 0.09f), p.skin);
            rig.LeftLowerArm = Limb(rig.LeftUpperArm, "LeftLowerArm", new Vector3(0f, -0.22f, 0f), new Vector3(0.075f, 0.20f, 0.075f), p.skin);
            rig.LeftHand = Limb(rig.LeftLowerArm, "LeftHand", new Vector3(0f, -0.16f, 0.02f), new Vector3(0.07f, 0.08f, 0.08f), p.skin);

            if (p.tattoos)
                MeshOn(rig.RightUpperArm, "Tattoo", PrimitiveType.Cube, new Vector3(0.045f, -0.02f, 0f), new Vector3(0.02f, 0.12f, 0.08f), new Color(0.12f, 0.08f, 0.08f));

            MeshOn(rig.Hips, "Shorts", PrimitiveType.Cube, new Vector3(0f, -0.08f, 0f), new Vector3(0.30f * bulk, 0.16f, 0.18f), shorts);
            rig.RightUpperLeg = Limb(rig.Hips, "RightUpperLeg", new Vector3(0.09f, -0.18f, 0f), new Vector3(0.11f * bulk, 0.26f, 0.11f), p.skin);
            rig.RightLowerLeg = Limb(rig.RightUpperLeg, "RightLowerLeg", new Vector3(0f, -0.26f, 0f), new Vector3(0.09f, 0.24f, 0.09f), p.skin);
            MeshOn(rig.RightLowerLeg, "RightSock", PrimitiveType.Cube, new Vector3(0f, -0.16f, 0f), new Vector3(0.09f, 0.10f, 0.09f), p.socks);
            MeshOn(rig.RightLowerLeg, "RightShoe", PrimitiveType.Cube, new Vector3(0f, -0.26f, 0.04f), new Vector3(0.11f, 0.07f, 0.20f), p.shoes);

            rig.LeftUpperLeg = Limb(rig.Hips, "LeftUpperLeg", new Vector3(-0.09f, -0.18f, 0f), new Vector3(0.11f * bulk, 0.26f, 0.11f), p.skin);
            rig.LeftLowerLeg = Limb(rig.LeftUpperLeg, "LeftLowerLeg", new Vector3(0f, -0.26f, 0f), new Vector3(0.09f, 0.24f, 0.09f), p.skin);
            MeshOn(rig.LeftLowerLeg, "LeftSock", PrimitiveType.Cube, new Vector3(0f, -0.16f, 0f), new Vector3(0.09f, 0.10f, 0.09f), p.socks);
            MeshOn(rig.LeftLowerLeg, "LeftShoe", PrimitiveType.Cube, new Vector3(0f, -0.26f, 0.04f), new Vector3(0.11f, 0.07f, 0.20f), p.shoes);
            rig.LeftFoot = Pivot(rig.LeftLowerLeg, "LeftFoot", new Vector3(0f, -0.26f, 0f));
            rig.RightFoot = Pivot(rig.RightLowerLeg, "RightFoot", new Vector3(0f, -0.26f, 0f));
            rig.BallAttach = CreateEmpty(rig.RightHand, "BallAttach", new Vector3(0f, -0.08f, 0.1f));
            MixamoBindPose.Capture(rig, rootGo.transform);

            return rig;
        }

        static bool TryBuildImported(CharacterDefinition def, CharacterVisualProfile p, Transform root, HumanoidRig rig)
        {
            GameObject prefab = def != null ? def.CustomVisualPrefab : null;
            if (prefab == null)
                prefab = ImportedPlayerVisual.LoadFor(def);
            if (prefab == null)
                return false;

            GameObject visual = Object.Instantiate(prefab, root);
            visual.name = "ImportedVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            DisableImportedAnimators(visual);
            EnsureUrpMaterials(visual);
            FitImportedToHeight(visual.transform, p.height);
            BindImportedSkeleton(visual.transform, rig);
            MixamoBindPose.Capture(rig, root);

            if (rig.Hips == null)
                rig.Hips = visual.transform;
            if (rig.Head == null)
                rig.Head = visual.transform;
            if (rig.RightHand == null)
                rig.RightHand = CreateEmpty(root, "RightHand", new Vector3(0.28f, p.height * 0.78f, 0.22f));

            if (rig.RightHand != null)
                rig.BallAttach = CreateEmpty(rig.RightHand, "BallAttach", new Vector3(0f, 0.11f, 0.045f));

            Bounds bounds = EncapsulateRenderers(visual);
            if (bounds.size.y > 0.01f)
                rig.Height = bounds.size.y;

            rig.ImportedSkeleton = true;
            return true;
        }

        static void DisableImportedAnimators(GameObject visual)
        {
            Animator[] animators = visual.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (Application.isPlaying)
                    Object.Destroy(animators[i]);
                else
                    Object.DestroyImmediate(animators[i]);
            }
        }

        static void EnsureUrpMaterials(GameObject visual)
        {
            Shader lit = MaterialFactory.Lit;
            if (lit == null)
                return;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Material[] mats = renderers[r].materials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material src = mats[i];
                    if (src == null || src.shader == null)
                        continue;
                    string shaderName = src.shader.name;
                    if (!shaderName.Contains("Standard") && !shaderName.Contains("Autodesk") && !shaderName.Contains("Legacy") && shaderName != "Hidden/InternalErrorShader")
                        continue;

                    Color color = src.HasProperty("_Color") ? src.color : Color.white;
                    if (src.HasProperty("_BaseColor"))
                        color = src.GetColor("_BaseColor");
                    Texture tex = src.mainTexture;
                    if (tex == null && src.HasProperty("_MainTex"))
                        tex = src.GetTexture("_MainTex");
                    if (tex == null && src.HasProperty("_BaseMap"))
                        tex = src.GetTexture("_BaseMap");
                    var litMat = new Material(lit) { name = src.name + "_URP", color = color };
                    if (litMat.HasProperty("_BaseColor"))
                        litMat.SetColor("_BaseColor", color);
                    if (tex != null)
                    {
                        litMat.mainTexture = tex;
                        if (litMat.HasProperty("_BaseMap"))
                            litMat.SetTexture("_BaseMap", tex);
                        if (litMat.HasProperty("_MainTex"))
                            litMat.SetTexture("_MainTex", tex);
                    }
                    mats[i] = litMat;
                    changed = true;
                }
                if (changed)
                    renderers[r].materials = mats;

                if (renderers[r] is SkinnedMeshRenderer skin)
                    skin.updateWhenOffscreen = true;
            }
        }

        static void FitImportedToHeight(Transform visual, float targetHeight)
        {
            Bounds bounds = EncapsulateRenderers(visual.gameObject);
            if (bounds.size.sqrMagnitude < 0.0001f)
                return;

            if (bounds.size.y < Mathf.Max(bounds.size.x, bounds.size.z) * 0.45f)
            {
                visual.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                bounds = EncapsulateRenderers(visual.gameObject);
            }

            float sizeY = Mathf.Max(0.05f, bounds.size.y);
            float scale = targetHeight / sizeY;
            visual.localScale = visual.localScale * scale;

            bounds = EncapsulateRenderers(visual.gameObject);
            Vector3 origin = visual.parent != null ? visual.parent.position : visual.position;
            visual.position += new Vector3(origin.x - bounds.center.x, origin.y - bounds.min.y, origin.z - bounds.center.z);
        }

        static Bounds EncapsulateRenderers(GameObject visual)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds(visual.transform.position, Vector3.zero);
            bool any = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled)
                    continue;
                if (!any)
                {
                    bounds = renderers[i].bounds;
                    any = true;
                }
                else
                    bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        static void BindImportedSkeleton(Transform visual, HumanoidRig rig)
        {
            rig.Hips = FindBone(visual, "Hips", "Pelvis", "hips", "pelvis");
            rig.Spine = FindBone(visual, "Spine", "spine");
            rig.Chest = FindBone(visual, "Spine2", "Chest", "Spine1", "spine2", "chest");
            if (rig.Chest == null)
                rig.Chest = rig.Spine;
            rig.Neck = FindBone(visual, "Neck");
            rig.Head = FindBone(visual, "Head");
            rig.LeftShoulder = FindBone(visual, "LeftShoulder");
            rig.RightShoulder = FindBone(visual, "RightShoulder");
            rig.LeftUpperArm = FindBone(visual, "LeftArm", "LeftUpperArm", "upper_arm.L", "upperarm_l");
            rig.LeftLowerArm = FindBone(visual, "LeftForeArm", "LeftLowerArm", "forearm_l", "lowerarm_l");
            rig.LeftHand = FindBone(visual, "LeftHand", "hand_l");
            rig.RightUpperArm = FindBone(visual, "RightArm", "RightUpperArm", "upper_arm.R", "upperarm_r");
            rig.RightLowerArm = FindBone(visual, "RightForeArm", "RightLowerArm", "forearm_r", "lowerarm_r");
            rig.RightHand = FindBone(visual, "RightHand", "hand_r");
            rig.LeftUpperLeg = FindBone(visual, "LeftUpLeg", "LeftUpperLeg", "thigh_l");
            rig.LeftLowerLeg = FindBone(visual, "LeftLeg", "LeftLowerLeg", "calf_l");
            rig.LeftFoot = FindBone(visual, "LeftFoot", "foot_l");
            rig.RightUpperLeg = FindBone(visual, "RightUpLeg", "RightUpperLeg", "thigh_r");
            rig.RightLowerLeg = FindBone(visual, "RightLeg", "RightLowerLeg", "calf_r");
            rig.RightFoot = FindBone(visual, "RightFoot", "foot_r");
        }

        static Transform FindBone(Transform root, params string[] keys)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int k = 0; k < keys.Length; k++)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    if (BoneNameMatches(all[i].name, keys[k]))
                        return all[i];
                }
            }
            return null;
        }

        static bool BoneNameMatches(string boneName, string key)
        {
            if (string.Equals(boneName, key, System.StringComparison.OrdinalIgnoreCase))
                return true;
            string leaf = boneName;
            int colon = boneName.LastIndexOf(':');
            if (colon >= 0 && colon < boneName.Length - 1)
                leaf = boneName.Substring(colon + 1);
            return string.Equals(leaf, key, System.StringComparison.OrdinalIgnoreCase);
        }

        static CharacterVisualProfile DefaultProfile()
        {
            return new CharacterVisualProfile
            {
                displayName = "Player",
                jerseyNumber = 0,
                skin = new Color(0.55f, 0.38f, 0.26f),
                hair = Color.black,
                jersey = Color.white,
                shorts = Color.black,
                trim = Color.red,
                shoes = Color.white,
                socks = Color.white,
                height = 1.95f,
                muscle = 1f,
                bulk = 1f
            };
        }

        static Transform Pivot(Transform parent, string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }

        static Transform MeshOn(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            UnityUtil.StripCollider(go);
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFactory.Opaque(color, 0.28f, 0f, name);
            return go.transform;
        }

        static Transform Limb(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color)
        {
            Transform pivot = Pivot(parent, name, localPos);
            MeshOn(pivot, name + "Mesh", PrimitiveType.Capsule, new Vector3(0f, -scale.y * 0.35f, 0f), scale, color);
            return pivot;
        }

        static void AddNumber(Transform chest, int number, Color color)
        {
            var go = new GameObject("JerseyNumber");
            go.transform.SetParent(chest, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, -0.11f);
            go.transform.localScale = Vector3.one * 0.08f;
            var tm = go.AddComponent<TextMesh>();
            tm.text = number.ToString();
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.font = FontUtil.Get();
            tm.characterSize = 0.4f;
        }

        static Transform CreateEmpty(Transform parent, string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }
    }
}
