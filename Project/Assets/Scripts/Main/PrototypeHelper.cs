using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class PrototypeHelper : MonoBehaviour
{
    [SerializeField] private bool ShowPrototypeInfo = true;
    [SerializeField] private GameObject prototypesHousing;
    [SerializeField] [Range(5, 20)] public int viewableConstraintDistance = 8;

    private void OnDrawGizmos()
    {
        if (!ShowPrototypeInfo) return;
        if (prototypesHousing == null) return;

        foreach (Transform item in prototypesHousing.transform)
        {
            if (item.parent != prototypesHousing.transform) return;

            Vector3 position = item.position;
            Vector3 labelPosition = position;

            if ((Camera.current.transform.position - position).magnitude < viewableConstraintDistance)
            {
                GUI.color = Color.white;
                ModulePrototype modulePrototype = item.gameObject.GetComponent<ModulePrototype>();

                string socketForwards;
                string socketLeft;
                string socketBackwards;
                string socketRight;

                int rotation = Mathf.RoundToInt(modulePrototype.gameObject.transform.eulerAngles.y);

                if (rotation == 0)
                {
                    socketForwards = modulePrototype.ForwardsSocket.ToString();
                    socketLeft = modulePrototype.LeftSocket.ToString();
                    socketBackwards = modulePrototype.BackwardsSocket.ToString();
                    socketRight = modulePrototype.RightSocket.ToString();
                }
                else if (rotation == 90)
                {
                    socketForwards = modulePrototype.LeftSocket.ToString();
                    socketLeft = modulePrototype.BackwardsSocket.ToString();
                    socketBackwards = modulePrototype.RightSocket.ToString();
                    socketRight = modulePrototype.ForwardsSocket.ToString();
                }
                else if (rotation == 180 || rotation == -180)
                {
                    socketForwards = modulePrototype.BackwardsSocket.ToString();
                    socketLeft = modulePrototype.RightSocket.ToString();
                    socketBackwards = modulePrototype.ForwardsSocket.ToString();
                    socketRight = modulePrototype.LeftSocket.ToString();
                }
                else if (rotation == -90 || rotation  == 270)
                {
                    socketForwards = modulePrototype.RightSocket.ToString();
                    socketLeft = modulePrototype.ForwardsSocket.ToString();
                    socketBackwards = modulePrototype.LeftSocket.ToString();
                    socketRight = modulePrototype.BackwardsSocket.ToString();
                }
                else
                {
                    socketForwards = "error";
                    socketLeft = "error";
                    socketBackwards = "error";
                    socketRight = "error";
                }

                labelPosition.x += 1;
                Handles.Label(labelPosition, socketForwards);

                labelPosition.x -= 2;
                Handles.Label(labelPosition, socketBackwards);

                labelPosition.x += 1;
                labelPosition.z += 1;
                Handles.Label(labelPosition, socketLeft);

                labelPosition.z -= 2;
                Handles.Label(labelPosition, socketRight);

                GUI.color = Color.yellow;

                labelPosition.z += 1;
                labelPosition.y += 1;
                Handles.Label(labelPosition, modulePrototype.UpwardsSocket.ToString());

                labelPosition.y -= 2;
                Handles.Label(labelPosition, modulePrototype.DownwardsSocket.ToString());

                GUI.color = Color.blue;

                Handles.Label(position, modulePrototype.modulePrototypeID.ToString());
            }
        }
    }
}
