using UnityEngine;

// Shared plane-raycast math: screen point -> local point on the board, no collider needed.
// Used by both PostItPlacementTester (dev tool) and PostItCreationFlow (real feature).
public static class BoardRaycaster
{
    public static bool TryGetLocalPoint(BoardCanvas board, Camera camera, Vector2 screenPosition, float surfaceOffset, out Vector3 localPoint)
    {
        Transform boardTransform = board.transform;
        Plane boardPlane = new Plane(boardTransform.up, boardTransform.position);
        Ray ray = camera.ScreenPointToRay(screenPosition);

        if (boardPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            localPoint = boardTransform.InverseTransformPoint(worldPoint);
            localPoint.y += surfaceOffset;
            return true;
        }

        localPoint = default;
        return false;
    }
}
