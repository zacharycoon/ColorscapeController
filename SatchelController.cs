using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ColorscapesColor
{
    Red,
    Orange,
    Yellow,
    Green,
    Blue,
    Indigo,
    Violet,
    UltraViolet,
    Rainbow,
    None
}

public enum PlayerState
{
    normal,
    hanging,
    climbing,
    stopped
}

[RequireComponent(typeof(CollisionInformation))]
public class SatchelController : MonoBehaviour {
    public Animator animator;
    // X movement
    [Tooltip("how fast can you get to max speed in the x direction in uu/s/s")]
    public float x_acceleration;
    [Tooltip("how fast can you get to max speed in the x direction in uu/s/s")]
    public float x_air_acceleration;
    [Tooltip("max walk speed and air speed in the x direction in uu/s")]
    public float max_walk_speed;
    [Tooltip("max run speed and air speed in the x direction in uu/s")]
    public float max_run_speed;

    // Y movement (Jumping)
    [Tooltip("How high the player can jump in Unity Units.")]
    public float jump_height = 1;
    [Tooltip("How long in seconds it takes to reach the jump height after starting a jump")]
    public float time_to_jump_height = 1;
    [Tooltip("While falling, how much should we multiply the fall_acceleration (any number higher than 1 makes the" +
       " character fall faster than they rised, below 1 they fall slower than they rise, 1 is unchanged)")]
    public float falling_modifier;
    [Tooltip("The max speed the player can fall in Unity Units per second")]
    public float max_fall_speed = 1f;
    [Tooltip("How long after the player falls off an edge that the jump input can still be pressed")]
    public float air_jump_time = .15f;
    [Tooltip("The time between pressing the jump button and actually firing the jump." +
        "\n\nIf the player releases the jump button in this time, they will perform a weaker jump." +
        "\n\nPressing the jump button will start this time regardless if the player is on the ground or not." +
        "However, the jump will only fire if they are on the ground by the end of the startup time " +
        "(This gives the player a bit of buffer to be able press jump just before they hit the ground, even though" +
        "they aren't on the ground.")]
    public float max_jump_startup_time;

    // Damping
    [Range (0,1)]
    public float x_damping_stopping;
    [Range(0, 1)]
    public float x_damping_turning;
    [Range(0, 1)]
    public float x_damping_basic;
    [Range(0, 1)]
    public float x_air_damping_stopping;
    [Range(0, 1)]
    public float x_air_damping_turning;
    [Range(0, 1)]
    public float x_air_damping_basic;

    [Tooltip("how many units do colorscapes spawn from your center?")]
    public float range;
    public GameObject[] colorscapes_shape1;
    public float colorscape_windup_time = .1f;
    public float colorscape_cooldown_time = .3f;
    public float colorscape_force_x = 1f;
    public float colorscape_force_y = 2f;

    [Tooltip("The time it takes to perform a climb.")]
    public float climbing_time = .6f;
    

    public bool has_paintbrush = false;
    public bool has_climbing = false;
    public bool has_sprint = false;
    public bool has_double_jump = false;

    CollisionInformation collisionInformation;

    // Colorscape information
    public bool[] colors_avaliable;

    private bool can_colorscape = true;
    private bool colorscaping_left = false;
    private bool colorscaping_right = false;
    private bool colorscaping_up = false;
    private bool colorscaping_down = false;
    private string colorscape_input = "";
    private float colorscape_timer = 0;
    private ColorscapesColor colorscape_color;
    private Vector3 colorscape_direction;

    // Movement speed information
    private float x_speed = 0;
    private float y_speed = 0;
    private Vector2 actual_velocity;
    private float current_max_speed;


    // Direction information
    private bool is_going_up = false;
    private bool is_going_down = false;
    private bool is_going_left = false;
    private bool is_going_right = false;
    private int direction_facing = 1; // positive is right, negative is left

    //Jumping information
    private float gravity = 0;
    private float initial_jump_velocity_y = 0;
    private float jump_velocity_y;
    private bool is_jumping = false;
    private bool is_jump_being_held = false;
    private float time_jump_button_held = 0;
    private float time_until_jump = 0;
    private float jump_force = 0;
    private float air_jump_timer = 0;
    private float jump_buffer_timer;

    // Climbing information 
    private float climb_timer = 0;

    // Player State
    private PlayerState playerState = PlayerState.normal;

    // Use this for initialization
    void Start()
    {
        nullColorPallet();
        collisionInformation = GetComponent<CollisionInformation>();

        gravity = -(2 * jump_height) / Mathf.Pow(time_to_jump_height, 2);
        initial_jump_velocity_y = (2 * jump_height) / time_to_jump_height;
        current_max_speed = max_walk_speed;
    }

    // Update is called once per frame
    void Update()
    {
        collisionInformation.updateDistances();

        switch(playerState)
        {
            case PlayerState.normal:
                normalMovement();
                break;
            case PlayerState.hanging:
            case PlayerState.climbing:
                climbingMovement();
                break;
            case PlayerState.stopped:
                actual_velocity.x = 0;
                actual_velocity.y = 0;
                x_speed = 0;
                y_speed = 0;
                break;

        }
        updatePlayerDirection();
        transform.Translate(actual_velocity.x, actual_velocity.y, 0);


        animator.SetFloat("speed_x", actual_velocity.x);
        animator.SetFloat("speed_y", actual_velocity.y);
        animator.SetBool("is_jumping", is_jumping);
        animator.SetBool("is_grounded", collisionInformation.isGrounded());
        animator.SetBool("can_colorscape", can_colorscape);
        animator.SetBool("colorscaping_down", colorscaping_down);
        animator.SetBool("colorscaping_left", colorscaping_left);
        animator.SetBool("colorscaping_right", colorscaping_right);
        animator.SetBool("colorscaping_up", colorscaping_up);
    }

    public void setPlayerState(PlayerState newState)
    {
        playerState = newState;
    }

    public PlayerState getPlayerState()
    {
        return playerState;
    }

    public void normalMovement()
    {
        float x_axis = Input.GetAxisRaw("Horizontal");
        x_speed = calculateXSpeed(x_axis, x_speed);
        y_speed = calculateYSpeed(x_speed, y_speed, x_axis);

        actual_velocity.x = x_speed;
        actual_velocity.y = y_speed;


        collisionInformation.updateSlopes(actual_velocity);

        if (collisionInformation.isGrounded()
            && (collisionInformation.isClimbingSlope() || collisionInformation.isDecendingSlope())
            && !is_jumping)
        {
            actual_velocity = slope(actual_velocity);
        }

        checkSummonColorscapes();

        actual_velocity = updateSpeedWithCollision(actual_velocity);
    }

    public void climbingMovement()
    {
        actual_velocity.x = 0;
        actual_velocity.y = 0;
        x_speed = 0;
        y_speed = 0;
        jump_velocity_y = 0;

        float y_axis = Input.GetAxisRaw("Vertical");

        if (y_axis >= .3 && playerState == PlayerState.hanging)
        {
            playerState = PlayerState.climbing;
        }
        else if (y_axis <= -.3 && playerState == PlayerState.hanging)
        {
            playerState = PlayerState.normal;
        }
        else if (playerState == PlayerState.climbing)
        {
            if (climb_timer >= climbing_time)
            {
                climb_timer = 0;
                playerState = PlayerState.normal;
                actual_velocity.y = collisionInformation.getHeight();
                actual_velocity.x = collisionInformation.getWidth() * direction_facing;
            }
            else
            {
                climb_timer += Time.deltaTime;
            }
        }
    }

    public void checkSummonColorscapes()
    {
        if(!has_paintbrush)
        {
            return;
        }

        if(can_colorscape)
        {
            if (collisionInformation.isGrounded() && Input.GetButtonDown("Left") && colors_avaliable[(int)ColorscapesColor.Blue])
            {
                startColorscapeWindup("Left", ColorscapesColor.Blue);
            }

            if (collisionInformation.isGrounded() && Input.GetButtonDown("Right") && colors_avaliable[(int)ColorscapesColor.Violet])
            {
                startColorscapeWindup("Right", ColorscapesColor.Violet);
            }

            if (collisionInformation.isGrounded() && Input.GetButtonDown("Up") && colors_avaliable[(int)ColorscapesColor.Indigo])
            {
                startColorscapeWindup("Up", ColorscapesColor.Indigo);
            }

            if (!collisionInformation.isGrounded() && Input.GetButtonDown("Down") && colors_avaliable[(int)ColorscapesColor.Green])
            {
                startColorscapeWindup("Down", ColorscapesColor.Green);
            }

            if (!collisionInformation.isGrounded() && Input.GetButtonDown("Up") && colors_avaliable[(int)ColorscapesColor.Orange])
            {
                startColorscapeWindup("Up", ColorscapesColor.Orange);
            }

            if (!collisionInformation.isGrounded() && Input.GetButtonDown("Left") && colors_avaliable[(int)ColorscapesColor.Red])
            {
                startColorscapeWindup("Left", ColorscapesColor.Red);
            }

            if (!collisionInformation.isGrounded() && Input.GetButtonDown("Right") && colors_avaliable[(int)ColorscapesColor.Yellow])
            {
                startColorscapeWindup("Right", ColorscapesColor.Yellow);
            }
        }
        else
        {
            updateColorscapeTimer();
        }
    }

    public float calculateXSpeed(float x_axis, float current_x_speed)
    {
        if (collisionInformation.isGrounded())
        {
            is_jumping = false;
            current_x_speed += x_acceleration * x_axis * Time.deltaTime * Time.deltaTime;
            if (Mathf.Abs(x_axis) < 0.01f)
            {
                current_x_speed *= Mathf.Pow(1f - x_damping_stopping, Time.deltaTime);
            }
            else if (Mathf.Sign(x_axis) != Mathf.Sign(current_x_speed))
            {
                current_x_speed *= Mathf.Pow(1f - x_damping_turning, Time.deltaTime);
            }
            else
            {
                current_x_speed *= Mathf.Pow(1f - x_damping_basic, Time.deltaTime);
            }

            if(current_x_speed < .0001 && current_x_speed > -.0001)
            {
                return 0;
            }

            if(Input.GetButton("Sprint") && has_sprint)
            {
                current_max_speed = max_run_speed;
                return clampSpeed(current_x_speed, max_run_speed * Time.deltaTime);
            }
            else
            {
                if(current_max_speed  > max_walk_speed)
                {
                    current_max_speed -= x_acceleration;
                }
                else
                {
                    current_max_speed = max_walk_speed;
                }
                return clampSpeed(current_x_speed, current_max_speed * Time.deltaTime);
            }
        }
        else
        {
            current_x_speed += x_air_acceleration * x_axis * Time.deltaTime * Time.deltaTime;
            if (Mathf.Abs(x_axis) < 0.01f)
            {
                current_x_speed *= Mathf.Pow(1f - x_air_damping_stopping, Time.deltaTime);
            }
            else if (Mathf.Sign(x_axis) != Mathf.Sign(current_x_speed))
            {
                current_x_speed *= Mathf.Pow(1f - x_air_damping_turning, Time.deltaTime);
            }
            else
            {
                current_x_speed *= Mathf.Pow(1f - x_air_damping_basic, Time.deltaTime);
            }

            if (Input.GetButton("Sprint") && has_sprint)
            {
                current_max_speed = max_run_speed;
                return clampSpeed(current_x_speed, max_run_speed * Time.deltaTime);
            }
            else
            {
                if  (current_max_speed > max_walk_speed)
                {
                    current_max_speed -= x_air_acceleration;
                }
                else
                {
                    current_max_speed = max_walk_speed;
                }
                return clampSpeed(current_x_speed, max_walk_speed * Time.deltaTime);
            }
        }
    }

    public float calculateYSpeed(float current_x_speed, float current_y_speed, float x_axis)
    {
        //Air movement and y collisions
        if (!collisionInformation.isGrounded())
        {
            current_y_speed = (jump_velocity_y * Time.deltaTime) + (.5f * gravity * Mathf.Pow(Time.deltaTime, 2));
            if (isFalling())
                current_y_speed *= falling_modifier;
            jump_velocity_y += gravity * Time.deltaTime;
            air_jump_timer += Time.deltaTime;
            current_y_speed = clampSpeed(current_y_speed, max_fall_speed * Time.deltaTime);
        }
        else
        {
            air_jump_timer = 0;
            current_y_speed = 0;
            jump_velocity_y = 0;
        }

        //Jumping

       // Once the player presses the button to jump, start counting down until the player actually jumps.
       if (Input.GetButtonDown("Jump")
            && time_until_jump <= 0)
        {
            time_until_jump = max_jump_startup_time;
            is_jump_being_held = true;
        }

        //If it's not time to jump, but is getting ready to jump, continue to countdown.(and record if the jump button is still being held)
        if (time_until_jump > 0)
        {
            time_until_jump -= Time.deltaTime;

            if(is_jump_being_held)
                time_jump_button_held += Time.deltaTime;

            if (time_jump_button_held > max_jump_startup_time)
                time_jump_button_held = max_jump_startup_time;
        }

        // if the jump button is released, record that.
        if (Input.GetButtonUp("Jump") && is_jump_being_held)
        {
            is_jump_being_held = false;
        }

        // If the jump button was pressed, and it's time to jump, then jump
        // [max_jump_force * (time_jump_button_held/max_jump_startup_time) ]
        if (time_jump_button_held != 0 
            && time_until_jump <= 0
            && air_jump_timer <= air_jump_time
            && !collisionInformation.headCollision())
        {
            jump_force = initial_jump_velocity_y * (time_jump_button_held / max_jump_startup_time);
            time_jump_button_held = 0;
            jump_velocity_y = jump_force;
            current_y_speed = jump_force * Time.deltaTime;
            is_jumping = true;
        }
        else if(time_until_jump <= 0)
        {
            time_jump_button_held = 0;
        }


        return current_y_speed;
    }

    private Vector2 slope(Vector2 velocity)
    {
        float moveDistance = Mathf.Abs(velocity.x);
        float angle = 0;
        float y_velocity_modifier = 1;

        if(velocity.x < 0 && collisionInformation.getUpLeftAngle() != 0)
        {
            angle = collisionInformation.getUpLeftAngle();
        }
        else if (velocity.x < 0 && collisionInformation.getDownRightAngle() != 0)
        {
            angle = collisionInformation.getDownRightAngle();
            y_velocity_modifier = -1;
        }
        else if (velocity.x > 0 && collisionInformation.getUpRightAngle() != 0)
        {
            angle = collisionInformation.getUpRightAngle();
        }
        else if (velocity.x > 0 && collisionInformation.getDownLeftAngle() != 0)
        {
            angle = collisionInformation.getDownLeftAngle();
            y_velocity_modifier = -1;
        }

        if(angle != 0)
        { 
            velocity.y = Mathf.Sin(angle * Mathf.Deg2Rad) * moveDistance * y_velocity_modifier;
            velocity.x = Mathf.Cos(angle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(x_speed);
        }
        return velocity;
    }

    private Vector2 updateSpeedWithCollision(Vector2 velocity)
    {
        // These are here for debugging purposes
        isGoingLeft();
        isGoingRight();
        isGoingUp();
        isFalling();

        //X Collisions
        if (collisionInformation.leftCollision() && isGoingLeft())
            velocity.x = 0;
        else if (collisionInformation.rightCollision() && isGoingRight())
            velocity.x = 0;

        else if (Mathf.Abs(x_speed) >= collisionInformation.getShortestDistanceLeft() && isGoingLeft())
        {
            velocity.x = -collisionInformation.getShortestDistanceLeft();
            if(collisionInformation.isClimbingSlope() && collisionInformation.leftCollision())
            {
                velocity.y = Mathf.Tan(collisionInformation.getUpRightAngle() * Mathf.Deg2Rad) * Mathf.Abs(velocity.x);
            }
        }
        else if (Mathf.Abs(x_speed) >= collisionInformation.getShortestDistanceRight() && isGoingRight())
        {
            velocity.x = collisionInformation.getShortestDistanceRight();
            if (collisionInformation.isClimbingSlope() && collisionInformation.rightCollision())
            {
                velocity.y = Mathf.Tan(collisionInformation.getUpRightAngle() * Mathf.Deg2Rad) * Mathf.Abs(velocity.x);
            }

        }

        //Y collisions
        if (Mathf.Abs(y_speed) >= collisionInformation.getShortestDistanceDown() && isFalling() && !collisionInformation.isDecendingSlope())
        {
            velocity.y = -collisionInformation.getShortestDistanceDown();
        }
        else if (Mathf.Abs(y_speed) >= collisionInformation.getShortestDistanceUp() && isGoingUp())
        {
            velocity.y = collisionInformation.getShortestDistanceUp();
            if(collisionInformation.isClimbingSlope())
            {
                velocity.x = velocity.y / Mathf.Tan(collisionInformation.getUpRightAngle() * Mathf.Deg2Rad) * Mathf.Sign(velocity.x);
            }
        }

        if ((collisionInformation.headCollision() && isGoingUp()) // if you hit your head going up
            || (collisionInformation.isGrounded() && isFalling() && !collisionInformation.isDecendingSlope()))  // hit the floor while falling
        {
            velocity.y = 0;
            jump_velocity_y = 0;
        }

        if(collisionInformation.isGrounded() && collisionInformation.getShortestDistanceDown() < 0 && !isGoingUp())
        {
            velocity.y = Mathf.Abs(collisionInformation.getShortestDistanceDown());
        }
        return velocity;
    }

    private float getShortestDistance(float distance1, float distance2)
    {
        if (distance1 > distance2)
            return distance2;
        else
            return distance1;
    }

    public bool isGoingLeft()
    {
        if (actual_velocity.x < 0)
            is_going_left = true;
        else
            is_going_left = false;
        return is_going_left;
    }

    public bool isGoingRight()
    {
        if (actual_velocity.x > 0)
            is_going_right = true;
        else
            is_going_right = false;
        return is_going_right;
    }

    public bool isGoingUp()
    {
        if (actual_velocity.y > 0)
            is_going_up = true;
        else
            is_going_up = false;
        return is_going_up;
    }

    public bool isFalling()
    {
        if (actual_velocity.y < 0)
            is_going_down = true;
        else
            is_going_down = false;
        return is_going_down;
    }

    public float clampSpeed(float speed, float max_speed)
    {
        if (Mathf.Abs(speed) >= max_speed && speed > 0)
        {
            speed = max_speed;
        }
        else if (Mathf.Abs(speed) >= max_speed && speed < 0)
        {
            speed = -max_speed;
        }

        return speed;
    }

    public void startColorscapeWindup(string input, ColorscapesColor input_color)
    {
        can_colorscape = false;
        colorscape_timer = 0;
        colorscape_input = input;
        colorscape_color = input_color;
        

        switch (input)
        {
            case "Left":
                colorscaping_left = true;
                colorscape_direction = Vector3.left;
                break;
            case "Up":
                colorscaping_up = true;
                colorscape_direction = Vector3.up;
                break;
            case "Down":
                colorscaping_down = true;
                colorscape_direction = Vector3.down;
                break;
            case "Right":
                colorscaping_right = true;
                colorscape_direction = Vector3.right;
                break;
        }
    }

    public void updateColorscapeTimer ()
    {
        colorscape_timer += Time.deltaTime;

        if(colorscape_timer >= colorscape_windup_time 
            && (colorscaping_left || colorscaping_right || colorscaping_up || colorscaping_down))
        {

            colorscaping_left = false;
            colorscaping_right = false;
            colorscaping_up = false;
            colorscaping_down = false;
            summonColorscapeWithButton(colorscape_input, colorscape_direction, colorscape_color);

            if (!collisionInformation.isGrounded())
            {
                if(colorscape_direction == Vector3.up)
                {
                    jump_velocity_y = -colorscape_force_y;
                }
                else if(colorscape_direction == Vector3.down)
                {
                    jump_velocity_y = colorscape_force_y;
                }
                else if (colorscape_direction == Vector3.right)
                {
                    x_speed += -colorscape_force_x;
                }
                else if (colorscape_direction == Vector3.left)
                {
                    x_speed += colorscape_force_x;
                }
            }
        }

        if(colorscape_timer >= colorscape_cooldown_time)
        {
            can_colorscape = true;        
            colorscape_input = "";
            colorscape_timer = 0;
        }
    }

    public void summonColorscapeWithButton(String button, Vector3 direction, ColorscapesColor color)
    {       
        GameObject temp = Instantiate(colorscapes_shape1[(int) color], transform.position + (direction * range), Quaternion.identity);
        temp.GetComponent<Colorscape>().input = button;
        temp.GetComponent<Colorscape>().color = color;
        colors_avaliable[(int)color] = false;
    }

    public void resetColorPallet()
    {
        colors_avaliable = new bool[] { true, true, true, true, true, true, true };
    }

    public void gainColor(ColorscapesColor color)
    {
        colors_avaliable[(int)color] = true;
    }

    public bool[] getColorsAvalible()
    {
        return colors_avaliable;
    }

    public void nullColorPallet()
    {
        colors_avaliable = new bool[] { false, false, false, false, false, false, false };
    }

    public void setIsHanging(bool new_value)
    {
        if (has_climbing)
            playerState = PlayerState.hanging;
        else
            playerState = PlayerState.normal;
    }

    private void updatePlayerDirection()
    {
        if (isGoingLeft())
        {
            direction_facing = -1;
        }
        if (isGoingRight())
        {
            direction_facing = 1;
        }
    }
}
