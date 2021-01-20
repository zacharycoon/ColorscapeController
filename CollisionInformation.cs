using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CollisionInformation : MonoBehaviour
{
    [Range(2, 50)]
    public int up_ray_count = 2;

    [Range(2, 50)]
    public int down_ray_count = 2;

    [Range(2, 50)]
    public int left_ray_count = 2;

    [Range(2, 50)]
    public int right_ray_count = 2;

    public float max_slope_angle = 60;

    [Tooltip("Units between the ray's starting points, and when it actually starts counting distance." +
        "Setting this at 0 is not recommended because if the raycast starts inside a collider, then the raycast" +
        "won't be able to detect collision. Do not set beyond the player's height or width either as weird behavior will" +
        "occur")]
    public float raycast_breathing_room = .2f;

    public float height = 1.98f;
    public float width = 1;

    private float up_ray_spacing = 0;
    private float down_ray_spacing = 0;
    private float left_ray_spacing = 0;
    private float right_ray_spacing = 0;

    private float[] distances_up;
    private float[] distances_down;
    private float[] distances_left;
    private float[] distances_right;

    private float shortest_distance_up = 0;
    private float shortest_distance_down = 0;
    private float shortest_distance_left = 0;
    private float shortest_distance_right = 0;

    private bool is_grounded = false;
    private bool collision_left = false;
    private bool collision_right = false;
    private bool collision_up = false;

    private bool is_climbing_slope = false;
    private bool is_decending_slope = false;
    private float slope_up_right_angle = 0f;
    private float slope_up_left_angle = 0f;
    private float slope_down_left_angle = 0f;
    private float slope_down_right_angle = 0f;
    private float last_slope_angle;
    private Vector2 slope_normal_perpandicular;

    private const float MAX_DISTANCE = 10;

    private float hit_left_debug = 0;
    private float hit_right_debug = 0;

    Vector2 top_left_corner;
    Vector2 bottom_right_corner;

    // Use this for initialization
    void Start()
    {
        distances_up = new float[up_ray_count];
        distances_down = new float[down_ray_count];
        distances_left = new float[left_ray_count];
        distances_right = new float[right_ray_count];

        up_ray_spacing = width / (up_ray_count - 1);
        down_ray_spacing = width / (down_ray_count - 1);
        left_ray_spacing = height / (left_ray_count - 1);
        right_ray_spacing = height / (right_ray_count - 1);

        
    }

    public void updateDistances()
    {
        top_left_corner = new Vector2(transform.position.x - (width / 2), transform.position.y + (height / 2));
        bottom_right_corner = new Vector2(transform.position.x + (width / 2), transform.position.y - (height / 2));

        shortest_distance_up = MAX_DISTANCE;
        shortest_distance_down = MAX_DISTANCE;
        shortest_distance_left = MAX_DISTANCE;
        shortest_distance_right = MAX_DISTANCE;

        //transform.position + (spacing_direction * starting_distance)

        shortest_distance_up =  getDataFromRayDirection(top_left_corner, Vector2.up, Vector2.right, up_ray_spacing, ref distances_up);
        shortest_distance_down = getDataFromRayDirection(bottom_right_corner, Vector3.down, Vector2.left,  down_ray_spacing,  ref distances_down);
        shortest_distance_left = getDataFromRayDirection(top_left_corner, Vector3.left, Vector2.down, left_ray_spacing, ref distances_left);
        shortest_distance_right = getDataFromRayDirection(bottom_right_corner, Vector3.right, Vector3.up, right_ray_spacing, ref distances_right);

        updateCollisions();
    }

    public void updateSlopes(Vector2 velocity)
    {
        updateSideSlopes();
        updateDownSlopes(velocity.x);
    }

    public float getShortestDistanceUp()
    {
        return shortest_distance_up;
    }

    public float getShortestDistanceDown()
    {
        return shortest_distance_down;
    }

    public float getShortestDistanceLeft()
    {
        return shortest_distance_left;
    }

    public float getShortestDistanceRight()
    {
        return shortest_distance_right;
    }

    public bool headCollision()
    {
        return collision_up;
    }

    public bool leftCollision()
    {
        return collision_left;
    }

    public bool rightCollision()
    { 
        return collision_right;
    }

    public bool isGrounded()
    {
        return is_grounded;
    }

    public float getHeight()
    {
        return height;
    }

    public float getWidth()
    {
        return width;
    }

    private void updateCollisions()
    {
        if (shortest_distance_down < .02f && shortest_distance_down >= -.02f)
            is_grounded = true;
        else
            is_grounded = false;

        if (shortest_distance_right < .02f && shortest_distance_right >= -.02f)
            collision_right = true;
        else
            collision_right = false;

        if (shortest_distance_left < .02f && shortest_distance_left >= -.02f)
            collision_left = true;
        else
            collision_left = false;

        if (shortest_distance_up < .02f && shortest_distance_up >= -.02f)
            collision_up = true;
        else
            collision_up = false;
    }

    public bool isClimbingSlope()
    {
        return is_climbing_slope;
    }

    public bool isDecendingSlope()
    {
        return is_decending_slope;
    }

    public float getUpRightAngle()
    {
        return slope_up_right_angle;
    }

    public float getUpLeftAngle()
    {
        return slope_up_left_angle;
    }

    public float getDownLeftAngle()
    {
        return slope_down_left_angle;
    }

    public float getDownRightAngle()
    {
        return slope_down_right_angle;
    }

    private void updateSideSlopes()
    {
        RaycastHit2D hit_right = Physics2D.Raycast(bottom_right_corner, Vector2.right);
        RaycastHit2D hit_left = Physics2D.Raycast(bottom_right_corner + (width * Vector2.left), Vector2.left);

        //Draw rays for debugging purposes.
        Debug.DrawRay(bottom_right_corner, Vector2.right * 2, Color.magenta);
        Debug.DrawRay(bottom_right_corner + (width * Vector2.left), Vector3.left * 2, Color.magenta);

        slope_up_right_angle = 0f;
        slope_up_left_angle = 0.0f;

        hit_left_debug = hit_left.distance;
        hit_right_debug = hit_right.distance;

        if (hit_right.collider != null
             && hit_right.distance <= .075f)
        { 
            slope_up_right_angle = Vector2.Angle(hit_right.normal, Vector2.up);

            if(slope_up_right_angle <= max_slope_angle)
            {
                is_climbing_slope = true;
                collision_right = false;
            }
            return;
        }

        if (hit_left.collider != null
            && hit_left.distance <= .075f)
        {
            slope_up_left_angle = Vector2.Angle(hit_left.normal, Vector2.up);

            if (slope_up_left_angle <= max_slope_angle)
            {
                is_climbing_slope = true;
                collision_left = false;
            }
            return;
        }
        
        is_climbing_slope = false;
    }

    public void updateDownSlopes(float velocity_x)
    {

        //Starting from the center of the player, go down half the player's height, then go right half the width (bottom right corner of the player).
        Vector2 origin_right = transform.position + (Vector3.down * height / 2f) + (Vector3.right * width / 2);
        // Bottom Left corner of the player
        Vector2 origin_left = transform.position + (Vector3.down * height / 2f) + (Vector3.left * width / 2);

        RaycastHit2D hit_right = Physics2D.Raycast(origin_right, Vector2.down);
        RaycastHit2D hit_left = Physics2D.Raycast(origin_left, Vector2.down);

        Debug.DrawRay(origin_right, Vector3.down, Color.magenta);
        Debug.DrawRay(origin_left, Vector3.down, Color.magenta);
        
        bool temp_is_decending = false;
        if (hit_right.collider != null)
        {
            float temp_angle = Vector2.Angle(hit_right.normal, Vector2.up);

            if ((temp_angle != 0 && temp_angle <= max_slope_angle)
                && hit_right.distance - .1 <= Mathf.Tan(temp_angle * Mathf.Deg2Rad) * Mathf.Abs(velocity_x))
            {
                slope_down_right_angle = temp_angle;
                temp_is_decending = true;
                is_decending_slope = true;
                is_grounded = true;
            }
        }

        if (hit_left.collider != null)
        {
            float temp_angle = Vector2.Angle(hit_left.normal, Vector2.up);

            if ((temp_angle != 0 && temp_angle <= max_slope_angle)
                && hit_left.distance - .1 <= Mathf.Tan(temp_angle * Mathf.Deg2Rad) * Mathf.Abs(velocity_x))
            {
                slope_down_left_angle = temp_angle;
                temp_is_decending = true;
                is_decending_slope = true;
                is_grounded = true;
            }
        }


        if(!temp_is_decending)
        {
            slope_down_left_angle = 0;
            slope_down_right_angle = 0;
            is_decending_slope = false;
        }
    }


    /**
     * Draws a number of rays an equal amount of spacing from each other. These rays will all be within the height/width of the player.
     * Each ray will record the distance between the starting point and first collision. This function will return the shortest distance.
     */
    public float getDataFromRayDirection(Vector2 starting_point, Vector2 direction_of_ray, Vector2 direction_of_spacing, float spacing_distance, ref float[] distance_array)
    {
        float shortest_distance = MAX_DISTANCE;

        for (int i = 0; i < distance_array.Length; i++)
        {
            Vector2 origin = starting_point + (direction_of_spacing * (spacing_distance * i));

            RaycastHit2D hit = Physics2D.Raycast(origin, direction_of_ray);
            Debug.DrawRay(origin, direction_of_ray);

            if (hit.collider != null)
            {
                distance_array[i] = hit.distance - raycast_breathing_room; //Record all distances for debugging purposes
                if (hit.distance - raycast_breathing_room < shortest_distance) // Record which is the shortest distance
                    shortest_distance = hit.distance - raycast_breathing_room;
            }

        }

        return shortest_distance;
    }
}
